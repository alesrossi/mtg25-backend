using FluentAssertions;
using Core.Models.Identity;
using TestUtilities.Builders;

namespace UnitTests.Models;

public class AppUserRoundTests
{
    private readonly TestDataBuilder _testDataBuilder = new();

    [Fact]
    public void AppUserRound_WhenCreated_PreservesReferences()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser();
        var league = _testDataBuilder.CreateLeague("owner-id");
        var round = new Round
        {
            LeagueId = 1,
            League = league
        };

        // Act
        var userRound = new AppUserRound
        {
            UserId = user.Id,
            User = user,
            RoundId = 1,
            Round = round,
            Position = 1,
            Score = 3
        };

        // Assert
        userRound.User.Should().BeSameAs(user, "because user reference should be preserved");
        userRound.UserId.Should().Be(user.Id, "because user id should match the referenced user");
        userRound.Round.Should().BeSameAs(round, "because round reference should be preserved");
        userRound.RoundId.Should().Be(1, "because round id should match the referenced round");
        userRound.Position.Should().Be(1, "because position is stored on the join entity");
        userRound.Score.Should().Be(3, "because score is stored on the join entity");
    }
}
