using FluentAssertions;
using Core.Models;
using Xunit;

namespace UnitTests.Models;

public class DeckHistoryEntitiesTests
{
    [Fact]
    public void DeckTreeEntry_TotalQuantity_SumsMainAndSide()
    {
        var entry = new DeckTreeEntry
        {
            ScryfallId = "s1",
            MaindeckQuantity = 3,
            SideboardQuantity = 2
        };

        entry.TotalQuantity.Should().Be(5);
    }

    [Fact]
    public void DeckCommit_WhenCreated_InitializesParentCollections()
    {
        var commit = new DeckCommit
        {
            DeckId = 1,
            TreeId = 1,
            AuthorId = "user-1",
            Message = "Initial commit"
        };

        commit.ParentLinks.Should().NotBeNull();
        commit.ChildLinks.Should().NotBeNull();
        commit.ParentLinks.Should().BeEmpty();
        commit.ChildLinks.Should().BeEmpty();
    }

    [Fact]
    public void DeckBranch_HeadCommit_IsOptional()
    {
        var branch = new DeckBranch
        {
            DeckId = 1,
            Name = "main",
            CreatedByUserId = "user-1"
        };

        branch.HeadCommitId.Should().BeNull();
        branch.HeadCommit.Should().BeNull();
    }
}
