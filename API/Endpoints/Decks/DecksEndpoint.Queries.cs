using System.Security.Claims;
using API.Dtos.Decks;
using API.Logging;
using API.Services;
using Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Decks;

public static partial class DecksEndpoint
{
    private static async Task<IResult> GetAllDecksForUser(
        HttpContext context,
        [FromServices] IDeckService deckService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Decks.List";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier");
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, userId);
        logger.LogOperationStart(operation, new { userId });

        try
        {
            var deckDtos = await deckService.GetDecksForUserAsync(userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { deckDtos.Count });
            return Results.Ok(deckDtos);
        }
        catch (DeckServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { userId });
            return await MapDeckServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetDeckByIdAsync(
        int id,
        HttpContext context,
        [FromServices] IDeckService deckService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Decks.Get";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        using var scope = logger.BeginOperationScope(operation, id);
        logger.LogOperationStart(operation, new { id, IsAnonymous = string.IsNullOrEmpty(userId) });

        try
        {
            var deck = await deckService.GetDeckByIdAsync(id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id });
            return Results.Ok(deck);
        }
        catch (DeckServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return await MapDeckServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetDeckCardsAsync(
        int deckId,
        bool maindeckOnly = false,
        bool sideboardOnly = false,
        bool? ownedOnly = null,
        HttpContext context = null!,
        [FromServices] IDeckService deckService = null!,
        [FromServices] ILogger<DecksEndpointLogCategory> logger = null!,
        [FromServices] IMessageLocalizer messageLocalizer = null!,
        CancellationToken cancellationToken = default)
    {
        const string operation = "DeckCards.Query";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        using var scope = logger.BeginOperationScope(operation, deckId);
        logger.LogOperationStart(operation, new { deckId, maindeckOnly, sideboardOnly, ownedOnly, IsAnonymous = string.IsNullOrEmpty(userId) });

        try
        {
            var deckCards = await deckService.GetDeckCardsAsync(deckId, userId, maindeckOnly, sideboardOnly, ownedOnly, cancellationToken);
            logger.LogOperationSuccess(operation, new { deckId, deckCards.Count });
            return Results.Ok(deckCards);
        }
        catch (DeckServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { deckId, userId });
            return await MapDeckServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetDeckCardByIdAsync(
        int deckId,
        int id,
        HttpContext context,
        [FromServices] IDeckService deckService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "DeckCards.Get";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { deckId, id });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, id);
        logger.LogOperationStart(operation, new { deckId, id });

        try
        {
            var deckCard = await deckService.GetDeckCardByIdAsync(deckId, id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { deckId, id });
            return Results.Ok(deckCard);
        }
        catch (DeckServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { deckId, id, userId });
            return await MapDeckServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetMissingDeckCardsAsync(
        int deckId,
        HttpContext context,
        [FromServices] IDeckService deckService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "DeckCards.Missing";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { deckId });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, deckId);
        logger.LogOperationStart(operation, new { deckId });

        try
        {
            var missingCards = await deckService.GetMissingDeckCardsAsync(deckId, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { deckId, MissingCount = missingCards.Count });
            return Results.Ok(missingCards);
        }
        catch (DeckServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { deckId, userId });
            return await MapDeckServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> ExportDeckAsync(
        int deckId,
        HttpContext context,
        [FromServices] IDeckService deckService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Decks.Export";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        using var scope = logger.BeginOperationScope(operation, deckId);
        logger.LogOperationStart(operation, new { deckId, IsAnonymous = string.IsNullOrEmpty(userId) });

        try
        {
            var deckList = await deckService.ExportDeckAsync(deckId, userId, cancellationToken);
            if (string.IsNullOrWhiteSpace(deckList))
            {
                logger.LogOperationWarning(operation, "Deck has no cards", new { deckId });
            }
            var lineCount = string.IsNullOrWhiteSpace(deckList)
                ? 0
                : deckList.Split('\n').Length;
            logger.LogOperationSuccess(operation, new { deckId, Lines = lineCount });
            return Results.Ok(deckList);
        }
        catch (DeckServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { deckId, userId });
            return await MapDeckServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetDeckBranchesAsync(
        int deckId,
        HttpContext context,
        [FromServices] IDeckHistoryService deckHistoryService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Decks.Branches";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { deckId });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, deckId);
        logger.LogOperationStart(operation, new { deckId });

        try
        {
            var branches = await deckHistoryService.GetBranchesAsync(deckId, userId, cancellationToken);
            var dtos = branches.Select(MapBranchDto).ToList();
            logger.LogOperationSuccess(operation, new { deckId, Count = dtos.Count });
            return Results.Ok(dtos);
        }
        catch (DeckHistoryServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { deckId, userId });
            return await MapDeckHistoryServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetDeckCommitsAsync(
        int deckId,
        HttpContext context,
        [FromServices] IDeckHistoryService deckHistoryService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Decks.Commits";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { deckId });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, deckId);
        logger.LogOperationStart(operation, new { deckId });

        try
        {
            var commits = await deckHistoryService.GetCommitsAsync(deckId, userId, cancellationToken);
            var dtos = commits.Select(MapCommitDto).ToList();
            logger.LogOperationSuccess(operation, new { deckId, Count = dtos.Count });
            return Results.Ok(dtos);
        }
        catch (DeckHistoryServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { deckId, userId });
            return await MapDeckHistoryServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetDeckDiffAsync(
        int deckId,
        [FromQuery] int fromCommitId,
        [FromQuery] int toCommitId,
        HttpContext context,
        [FromServices] IDeckHistoryService deckHistoryService,
        [FromServices] ILogger<DecksEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Decks.Diff";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            logger.LogOperationWarning(operation, "Missing user identifier", new { deckId });
            return Results.Unauthorized();
        }

        using var scope = logger.BeginOperationScope(operation, deckId);
        logger.LogOperationStart(operation, new { deckId, fromCommitId, toCommitId });

        try
        {
            var diff = await deckHistoryService.DiffAsync(deckId, userId, fromCommitId, toCommitId, cancellationToken);
            var dto = MapDiffDto(diff);
            logger.LogOperationSuccess(operation, new { deckId, Added = dto.Added.Count, Removed = dto.Removed.Count, Modified = dto.Modified.Count });
            return Results.Ok(dto);
        }
        catch (DeckHistoryServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { deckId, userId });
            return await MapDeckHistoryServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> GetDeckHistoryVisualizationAsync(
        int deckId,
        [FromQuery] bool includeOrphans = false,
        HttpContext context = null!,
        [FromServices] IDeckHistoryService deckHistoryService = null!,
        [FromServices] ILogger<DecksEndpointLogCategory> logger = null!,
        [FromServices] IMessageLocalizer messageLocalizer = null!,
        CancellationToken cancellationToken = default)
    {
        const string operation = "Decks.History";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        using var scope = logger.BeginOperationScope(operation, deckId);
        logger.LogOperationStart(operation, new { deckId, includeOrphans, IsAnonymous = string.IsNullOrEmpty(userId) });

        try
        {
            var result = await deckHistoryService.GetHistoryVisualizationAsync(deckId, userId, includeOrphans, cancellationToken);
            var dto = MapHistoryVisualizationDto(result);
            logger.LogOperationSuccess(operation, new { deckId, dto.Metadata.TotalBranches, dto.Metadata.TotalCommits });
            return Results.Ok(dto);
        }
        catch (DeckHistoryServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { deckId, userId });
            return await MapDeckHistoryServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static DeckHistoryVisualizationDto MapHistoryVisualizationDto(DeckHistoryVisualizationResult result)
        => new()
        {
            DeckId = result.DeckId,
            Branches = result.BranchHistories.Select(bh => new BranchHistoryDto
            {
                BranchId = bh.Branch.Id,
                BranchName = bh.Branch.Name,
                HeadCommitId = bh.Branch.HeadCommitId,
                CreatedAt = bh.Branch.CreatedAt,
                CreatedByUserId = bh.Branch.CreatedByUserId,
                Commits = bh.Commits.Select(c => new CommitNodeDto
                {
                    Id = c.Id,
                    DeckId = c.DeckId,
                    TreeId = c.TreeId,
                    AuthorId = c.AuthorId,
                    Message = c.Message,
                    CommittedAt = c.CommittedAt,
                    ParentIds = result.ParentLookup.GetValueOrDefault(c.Id, []),
                    ReferencingBranches = result.CommitToBranches.GetValueOrDefault(c.Id, [])
                }).ToList()
            }).ToList(),
            OrphanedCommits = result.OrphanedCommits.Select(c => new CommitNodeDto
            {
                Id = c.Id,
                DeckId = c.DeckId,
                TreeId = c.TreeId,
                AuthorId = c.AuthorId,
                Message = c.Message,
                CommittedAt = c.CommittedAt,
                ParentIds = result.ParentLookup.GetValueOrDefault(c.Id, []),
                ReferencingBranches = []
            }).ToList(),
            Metadata = new CommitGraphMetadataDto
            {
                TotalCommits = result.Metadata.TotalCommits,
                TotalBranches = result.Metadata.TotalBranches,
                OrphanedCommits = result.Metadata.OrphanedCommits,
                EarliestCommit = result.Metadata.EarliestCommit,
                LatestCommit = result.Metadata.LatestCommit
            }
        };

    private static DeckBranchDto MapBranchDto(DeckBranch branch)
        => new()
        {
            Id = branch.Id,
            DeckId = branch.DeckId,
            Name = branch.Name,
            HeadCommitId = branch.HeadCommitId,
            CreatedByUserId = branch.CreatedByUserId,
            CreatedAt = branch.CreatedAt
        };

    private static DeckCommitDto MapCommitDto(DeckCommit commit)
        => new()
        {
            Id = commit.Id,
            DeckId = commit.DeckId,
            TreeId = commit.TreeId,
            AuthorId = commit.AuthorId,
            Message = commit.Message,
            CommittedAt = commit.CommittedAt
        };

    private static DeckDiffDto MapDiffDto(DeckDiffResult diff)
        => new()
        {
            Added = diff.Added.Select(MapDiffEntryDto).ToList(),
            Removed = diff.Removed.Select(MapDiffEntryDto).ToList(),
            Modified = diff.Modified.Select(MapDiffEntryDto).ToList()
        };

    private static DeckDiffEntryDto MapDiffEntryDto(DeckDiffChange change)
        => new()
        {
            ScryfallId = change.ScryfallId,
            OldMaindeckQuantity = change.OldMaindeckQuantity,
            OldSideboardQuantity = change.OldSideboardQuantity,
            NewMaindeckQuantity = change.NewMaindeckQuantity,
            NewSideboardQuantity = change.NewSideboardQuantity
        };
}
