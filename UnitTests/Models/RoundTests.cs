using FluentAssertions;
using Core.Models;
using Core.Models.Identity;
using TestUtilities.Builders;

namespace UnitTests.Models;

public class RoundTests
{
    private readonly TestDataBuilder _testDataBuilder = new();

    [Fact]
    public void Round_WhenCreated_HasValidInitialState()
    {
        // Arrange & Act
        var league = _testDataBuilder.CreateLeague("owner-id");
        var round = new Round
        {
            LeagueId = 1,
            League = league
        };

        // Assert
        round.Should().BeAssignableTo<BaseModel>("because Round inherits from BaseModel");
        round.Id.Should().Be(0, "because BaseModel's identity key is assigned on persistence");
        round.Status.Should().Be(Status.NotPlayed, "because NotPlayed is the default status");
        round.StartDate.Should().BeNull("because rounds start without a start date");
        round.Description.Should().BeNull("because rounds start without a description");
        round.Players.Should().NotBeNull("because players collection should be initialized");
        round.Players.Should().BeEmpty("because rounds start with no assigned players");
        round.League.Should().BeSameAs(league, "because round belongs to the specified league");
        round.LeagueId.Should().Be(1, "because round tracks its league foreign key");
    }

    [Fact]
    public void Round_WithPlayers_MaintainsRoundAssignments()
    {
        // Arrange
        var league = _testDataBuilder.CreateLeague("owner-id");
        var round = new Round
        {
            LeagueId = 1,
            League = league
        };
        var player1 = _testDataBuilder.CreateUser();
        var player2 = _testDataBuilder.CreateUser();

        // Act
        round.Players = new List<AppUserRound>
        {
            new() { UserId = player1.Id, User = player1, RoundId = 1, Round = round, Position = 1, Score = 3 },
            new() { UserId = player2.Id, User = player2, RoundId = 1, Round = round, Position = 2, Score = 1 }
        };

        // Assert
        round.Players.Should().HaveCount(2, "because two players were assigned to the round");
        round.Players.Should().OnlyContain(p => p.Round == round, "because all entries should reference this round");
        round.Players.Should().OnlyContain(p => p.RoundId == 1, "because all entries should use the round id");
    }
}
