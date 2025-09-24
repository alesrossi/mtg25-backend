using FluentAssertions;
using Core.Models;
using Core.Models.Identity;
using TestUtilities.Builders;

namespace UnitTests.Models;

/// <summary>
/// Tests for League entity business logic and behavior.
/// Focuses on domain rules and entity state management.
/// </summary>
public class LeagueTests
{
    private readonly TestDataBuilder _testDataBuilder;

    public LeagueTests()
    {
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public void League_WhenCreated_HasValidInitialState()
    {
        // Arrange & Act
        var ownerId = "test-owner-id";
        var league = _testDataBuilder.CreateLeague(ownerId);

        // Assert
        league.Id.Should().Be(0, "because new leagues are assigned an ID when persisted");
        league.OwnerId.Should().Be(ownerId, "because league should belong to the specified owner");
        league.Name.Should().NotBeNullOrEmpty("because league name is required");
        league.Code.Should().NotBeNullOrEmpty("because league code is required");
        league.Format.Should().NotBeNullOrEmpty("because format is required");
        league.TotalRounds.Should().BeGreaterThanOrEqualTo(0, "because total rounds cannot be negative");
        league.RoundsToConsider.Should().BeGreaterThanOrEqualTo(0, "because rounds to consider cannot be negative");
        league.MinimumRounds.Should().BeGreaterThanOrEqualTo(0, "because minimum rounds cannot be negative");
        league.TotalPlayers.Should().BeGreaterThanOrEqualTo(0, "because total players cannot be negative");
        league.PointsToGive.Should().NotBeNull("because points to give is required");
        league.IsActive.Should().BeTrue("because leagues are active by default");
        league.UserLeagues.Should().NotBeNull("because user leagues collection should be initialized");
    }

    [Theory]
    [InlineData("Standard")]
    [InlineData("Modern")]
    [InlineData("Legacy")]
    [InlineData("Vintage")]
    [InlineData("Pioneer")]
    [InlineData("Commander")]
    [InlineData("Draft")]
    [InlineData("Sealed")]
    public void League_WithDifferentFormats_AcceptsAllValidFormats(string format)
    {
        // Arrange & Act
        var league = _testDataBuilder.CreateLeague("owner-id");
        league.Format = format;

        // Assert
        league.Format.Should().Be(format, $"because {format} is a valid Magic format");
    }

    [Fact]
    public void League_WithPointsSystem_StoresCorrectly()
    {
        // Arrange & Act
        var league = _testDataBuilder.CreateLeague("owner-id");
        var pointsSystem = new List<int> { 3, 1, 0 }; // Win, Draw, Loss
        league.PointsToGive = pointsSystem;

        // Assert
        league.PointsToGive.Should().BeEquivalentTo(pointsSystem, 
            "because that's the points system we configured");
        league.PointsToGive.Should().HaveCount(3, "because we have points for win/draw/loss");
    }

    [Fact]
    public void League_WithUniqueCode_IdentifiesLeague()
    {
        // Arrange & Act
        var league1 = _testDataBuilder.CreateLeague("owner-id");
        league1.Code = "LEAGUE2024";

        var league2 = _testDataBuilder.CreateLeague("owner-id");
        league2.Code = "SPRING2024";

        // Assert
        league1.Code.Should().Be("LEAGUE2024", "because that's the code we assigned");
        league2.Code.Should().Be("SPRING2024", "because that's the code we assigned");
        league1.Code.Should().NotBe(league2.Code, "because each league should have a unique code");
    }

    [Fact]
    public void League_IsActiveByDefault_CanBeDeactivated()
    {
        // Arrange & Act
        var activeLeague = _testDataBuilder.CreateLeague("owner-id");
        var inactiveLeague = _testDataBuilder.CreateLeague("owner-id");
        inactiveLeague.IsActive = false;

        // Assert
        activeLeague.IsActive.Should().BeTrue("because leagues are active by default");
        inactiveLeague.IsActive.Should().BeFalse("because we explicitly deactivated this league");
    }

    [Fact]
    public void League_WithUserLeagues_MaintainsPlayerRelationships()
    {
        // Arrange & Act
        var league = _testDataBuilder.CreateLeague("owner-id");
        var userLeagues = new List<AppUserLeague>
        {
            new() { UserId = "player1", LeagueId = league.Id },
            new() { UserId = "player2", LeagueId = league.Id },
            new() { UserId = "player3", LeagueId = league.Id }
        };
        
        // Note: In a real scenario, this would be managed by the repository/context
        league.UserLeagues = userLeagues;

        // Assert
        league.UserLeagues.Should().HaveCount(3, "because 3 players joined the league");
        league.UserLeagues.Should().OnlyContain(ul => ul.LeagueId == league.Id,
            "because all user leagues should reference this league");
    }

    [Fact]
    public void League_BelongsToOwner_MaintainsOwnership()
    {
        // Arrange & Act
        var owner1 = "owner-1";
        var owner2 = "owner-2";

        var league1 = _testDataBuilder.CreateLeague(owner1);
        league1.Name = "Owner 1 Tournament";
        league1.Code = "O1TOURN";

        var league2 = _testDataBuilder.CreateLeague(owner2);
        league2.Name = "Owner 2 Tournament";
        league2.Code = "O2TOURN";

        // Assert
        league1.OwnerId.Should().Be(owner1, "because league1 belongs to owner1");
        league2.OwnerId.Should().Be(owner2, "because league2 belongs to owner2");
        league1.OwnerId.Should().NotBe(league2.OwnerId, "because leagues belong to different owners");
    }

    [Fact]
    public void League_InheritingFromBaseModel_HasBaseModelProperties()
    {
        // Arrange & Act
        var league = _testDataBuilder.CreateLeague("owner-id");

        // Assert
        league.Should().BeAssignableTo<BaseModel>("because League inherits from BaseModel");
        league.Id.Should().Be(0, "because BaseModel's identity key is assigned on persistence");
    }
}
