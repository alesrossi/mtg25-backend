using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Dtos.Cards;
using API.Services;
using Core.Enums;
using Core.Interfaces;
using Core.Models;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TestUtilities.Builders;
using TestUtilities.Database;
using TestUtilities.Scryfall;
using Xunit;

namespace UnitTests.Services;

public class DeckHistoryServiceTests
{
    private readonly TestDataBuilder _builder = new();

    [Fact]
    public async Task InitializeDeckHistoryAsync_WhenBranchExists_ThrowsConflict()
    {
        await using var context = InMemoryDbContextFactory.CreateMain();
        var unitOfWork = CreateUnitOfWork(context);
        var deck = SeedDeck(context, ownerId: "user-1");

        context.DeckBranches.Add(new DeckBranch
        {
            DeckId = deck.Id,
            Name = "main",
            CreatedByUserId = deck.OwnerId
        });
        await context.SaveChangesAsync();

        var service = CreateServiceWithCardData(context);

        Func<Task> act = () => service.InitializeDeckHistoryAsync(deck.Id, deck.OwnerId);

        var exception = await act.Should().ThrowAsync<DeckHistoryServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task CommitAsync_WhenBranchMissing_ThrowsNotFound()
    {
        await using var context = InMemoryDbContextFactory.CreateMain();
        var deck = SeedDeck(context, ownerId: "user-1");
        var service = CreateServiceWithCardData(context);

        Func<Task> act = () => service.CommitAsync(deck.Id, deck.OwnerId, "main", "Commit");

        var exception = await act.Should().ThrowAsync<DeckHistoryServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task CreateBranchAsync_WhenCommitExists_CreatesBranch()
    {
        await using var context = InMemoryDbContextFactory.CreateMain();
        var deck = SeedDeck(context, ownerId: "user-1");
        var service = CreateServiceWithCardData(context);

        context.DeckTrees.Add(new DeckTree());
        await context.SaveChangesAsync();

        var commit = new DeckCommit
        {
            DeckId = deck.Id,
            TreeId = context.DeckTrees.First().Id,
            AuthorId = deck.OwnerId,
            Message = "Root"
        };
        context.DeckCommits.Add(commit);
        await context.SaveChangesAsync();

        var branch = await service.CreateBranchAsync(deck.Id, deck.OwnerId, "new", commit.Id);

        branch.Name.Should().Be("new");
        branch.HeadCommitId.Should().Be(commit.Id);
    }

    [Fact]
    public async Task CheckoutAsync_RebuildsWorkingCopyFromCommit()
    {
        await using var context = InMemoryDbContextFactory.CreateMain();
        var deck = SeedDeck(context, ownerId: "user-1");
        var service = CreateServiceWithCardData(context, new[]
        {
            _builder.CreateOracleCard(id: "s1", name: "Alpha"),
            _builder.CreateOracleCard(id: "s2", name: "Beta")
        });

        context.DeckCards.AddRange(new[]
        {
            _builder.CreateDeckCard(deck.Id, oracleId: "s1", maindeckQuantity: 4),
            _builder.CreateDeckCard(deck.Id, oracleId: "s1", maindeckQuantity: 1),
            _builder.CreateDeckCard(deck.Id, oracleId: "s3", maindeckQuantity: 2)
        });
        await context.SaveChangesAsync();

        var tree = new DeckTree
        {
            Entries =
            [
                new DeckTreeEntry { ScryfallId = "s1", MaindeckQuantity = 2, SideboardQuantity = 0 },
                new DeckTreeEntry { ScryfallId = "s2", MaindeckQuantity = 1, SideboardQuantity = 1 }
            ]
        };
        context.DeckTrees.Add(tree);
        await context.SaveChangesAsync();

        var commit = new DeckCommit
        {
            DeckId = deck.Id,
            TreeId = tree.Id,
            AuthorId = deck.OwnerId,
            Message = "Restore"
        };
        context.DeckCommits.Add(commit);
        await context.SaveChangesAsync();

        await service.CheckoutAsync(deck.Id, deck.OwnerId, commit.Id);

        var deckCards = context.DeckCards.ToList();
        deckCards.Should().HaveCount(2);
        deckCards.Should().ContainSingle(c => c.ScryfallId == "s1" && c.MaindeckQuantity == 2 && c.SideboardQuantity == 0);
        deckCards.Should().ContainSingle(c => c.ScryfallId == "s2" && c.MaindeckQuantity == 1 && c.SideboardQuantity == 1);

        var updatedDeck = await context.Decks.FindAsync(deck.Id);
        updatedDeck!.NumberOfCards.Should().Be(4);
    }

    [Fact]
    public async Task CheckoutAsync_WhenCardDataMissing_ThrowsBadRequest()
    {
        await using var context = InMemoryDbContextFactory.CreateMain();
        var deck = SeedDeck(context, ownerId: "user-1");
        var service = CreateServiceWithCardData(context, Array.Empty<ScryfallCardDto>());

        var tree = new DeckTree
        {
            Entries = [new DeckTreeEntry { ScryfallId = "missing", MaindeckQuantity = 1, SideboardQuantity = 0 }]
        };
        context.DeckTrees.Add(tree);
        await context.SaveChangesAsync();

        var commit = new DeckCommit
        {
            DeckId = deck.Id,
            TreeId = tree.Id,
            AuthorId = deck.OwnerId,
            Message = "Restore"
        };
        context.DeckCommits.Add(commit);
        await context.SaveChangesAsync();

        Func<Task> act = () => service.CheckoutAsync(deck.Id, deck.OwnerId, commit.Id);

        var exception = await act.Should().ThrowAsync<DeckHistoryServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DiffAsync_WithChanges_ReturnsAddedRemovedModified()
    {
        await using var context = InMemoryDbContextFactory.CreateMain();
        var deck = SeedDeck(context, ownerId: "user-1");
        var service = CreateServiceWithCardData(context);

        var treeA = new DeckTree
        {
            Entries =
            [
                new DeckTreeEntry { ScryfallId = "s1", MaindeckQuantity = 2, SideboardQuantity = 0 },
                new DeckTreeEntry { ScryfallId = "s2", MaindeckQuantity = 1, SideboardQuantity = 0 }
            ]
        };
        var treeB = new DeckTree
        {
            Entries =
            [
                new DeckTreeEntry { ScryfallId = "s2", MaindeckQuantity = 2, SideboardQuantity = 0 },
                new DeckTreeEntry { ScryfallId = "s3", MaindeckQuantity = 1, SideboardQuantity = 1 }
            ]
        };
        context.DeckTrees.AddRange(treeA, treeB);
        await context.SaveChangesAsync();

        var commitA = new DeckCommit { DeckId = deck.Id, TreeId = treeA.Id, AuthorId = deck.OwnerId, Message = "A" };
        var commitB = new DeckCommit { DeckId = deck.Id, TreeId = treeB.Id, AuthorId = deck.OwnerId, Message = "B" };
        context.DeckCommits.AddRange(commitA, commitB);
        await context.SaveChangesAsync();

        var diff = await service.DiffAsync(deck.Id, deck.OwnerId, commitA.Id, commitB.Id);

        diff.Added.Should().ContainSingle(change => change.ScryfallId == "s3");
        diff.Removed.Should().ContainSingle(change => change.ScryfallId == "s1");
        diff.Modified.Should().ContainSingle(change => change.ScryfallId == "s2" && change.OldMaindeckQuantity == 1 && change.NewMaindeckQuantity == 2);
    }

    [Fact]
    public async Task DiffAsync_WhenCommitMissing_ThrowsNotFound()
    {
        await using var context = InMemoryDbContextFactory.CreateMain();
        var deck = SeedDeck(context, ownerId: "user-1");
        var service = CreateServiceWithCardData(context);

        Func<Task> act = () => service.DiffAsync(deck.Id, deck.OwnerId, 100, 200);

        var exception = await act.Should().ThrowAsync<DeckHistoryServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    private DeckHistoryService CreateServiceWithCardData(MainContext context, IEnumerable<ScryfallCardDto>? cards = null)
    {
        var cardService = CardDataServiceTestHelper.CreateWithCards(cards ?? Array.Empty<ScryfallCardDto>());
        return new DeckHistoryService(CreateUnitOfWork(context), cardService);
    }

    private static UnitOfWork CreateUnitOfWork(MainContext context)
        => new(context, NullLogger<UnitOfWork>.Instance, NullLoggerFactory.Instance);

    private Deck SeedDeck(MainContext context, string ownerId)
    {
        var deck = _builder.CreateDeck(ownerId, DeckFormat.Modern);
        context.Decks.Add(deck);
        context.SaveChanges();
        return deck;
    }

}
