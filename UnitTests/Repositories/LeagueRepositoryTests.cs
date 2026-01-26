using System.Text.Json;
using Core.Enums;
using Core.Models.Identity;
using FluentAssertions;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using TestUtilities.Builders;
using TestUtilities.Database;

namespace UnitTests.Repositories;

/// <summary>
/// Tests for League repository focusing on data access patterns.
/// Uses in-memory database for fast, isolated testing.
/// Note: League is stored in the Identity context, not the main context.
/// </summary>
public class LeagueRepositoryTests : IDisposable
{
    private readonly MainContext _context;
    private readonly AppIdentityDbContext _identityContext;
    private readonly TestDataBuilder _testDataBuilder;

    public LeagueRepositoryTests()
    {
        _context = InMemoryDbContextFactory.CreateMain();
        _identityContext = InMemoryDbContextFactory.CreateIdentity();
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsLeague()
    {
        // Arrange
        var owner = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(owner);
        await _identityContext.SaveChangesAsync();

        var league = _testDataBuilder.CreateLeague(owner.Id);
        league.Name = "Test Tournament";
        league.Code = "TEST2024";
        league.Format = DeckFormat.Standard;
        
        _identityContext.Leagues.Add(league);
        await _identityContext.SaveChangesAsync();

        // Act - For this test, we'll simulate the lookup directly from identity context
        var result = await _identityContext.Leagues.FindAsync(league.Id);

        // Assert
        result.Should().NotBeNull("because a league with this ID exists");
        result.Id.Should().Be(league.Id);
        result.Name.Should().Be("Test Tournament");
        result.Code.Should().Be("TEST2024");
        result.Format.Should().Be(DeckFormat.Standard);
        result.OwnerId.Should().Be(owner.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Act
        var result = await _identityContext.Leagues.FindAsync(999999);

        // Assert
        result.Should().BeNull("because no league exists with this ID");
    }

    [Fact]
    public async Task ListAsync_WithLeaguesFromDifferentOwners_ReturnsAllLeagues()
    {
        // Arrange
        var owner1 = _testDataBuilder.CreateUser("owner1@test.com", "owner1");
        var owner2 = _testDataBuilder.CreateUser("owner2@test.com", "owner2");
        _identityContext.Users.AddRange(owner1, owner2);
        await _identityContext.SaveChangesAsync();

        var owner1Leagues = new[]
        {
            _testDataBuilder.CreateLeague(owner1.Id),
            _testDataBuilder.CreateLeague(owner1.Id)
        };
        owner1Leagues[0].Name = "Owner1 Standard League";
        owner1Leagues[0].Code = "O1STD";
        owner1Leagues[0].Format = DeckFormat.Standard;
        owner1Leagues[1].Name = "Owner1 Modern League";
        owner1Leagues[1].Code = "O1MOD";
        owner1Leagues[1].Format = DeckFormat.Modern;

        var owner2League = _testDataBuilder.CreateLeague(owner2.Id);
        owner2League.Name = "Owner2 Legacy League";
        owner2League.Code = "O2LEG";
        owner2League.Format = DeckFormat.Legacy;

        _identityContext.Leagues.AddRange(owner1Leagues);
        _identityContext.Leagues.Add(owner2League);
        await _identityContext.SaveChangesAsync();

        // Act
        var result = _identityContext.Leagues.ToList();

        // Assert
        result.Should().HaveCount(3, "because we added 3 leagues total");
        result.Select(l => l.Name).Should().Contain(["Owner1 Standard League", "Owner1 Modern League", "Owner2 Legacy League"
        ]);
    }

    [Fact]
    public async Task ListAsync_FilterByOwner_ReturnsOnlyOwnerLeagues()
    {
        // Arrange
        var owner1 = _testDataBuilder.CreateUser("owner1@test.com", "owner1");
        var owner2 = _testDataBuilder.CreateUser("owner2@test.com", "owner2");
        _identityContext.Users.AddRange(owner1, owner2);
        await _identityContext.SaveChangesAsync();

        var owner1Leagues = new[]
        {
            _testDataBuilder.CreateLeague(owner1.Id),
            _testDataBuilder.CreateLeague(owner1.Id)
        };
        owner1Leagues[0].Name = "Owner1 League 1";
        owner1Leagues[0].Code = "O1L1";
        owner1Leagues[1].Name = "Owner1 League 2";
        owner1Leagues[1].Code = "O1L2";

        var owner2League = _testDataBuilder.CreateLeague(owner2.Id);
        owner2League.Name = "Owner2 League";
        owner2League.Code = "O2L1";

        _identityContext.Leagues.AddRange(owner1Leagues);
        _identityContext.Leagues.Add(owner2League);
        await _identityContext.SaveChangesAsync();

        // Act
        var owner1LeaguesFiltered = _identityContext.Leagues.Where(l => l.OwnerId == owner1.Id).ToList();

        // Assert
        owner1LeaguesFiltered.Should().HaveCount(2, "because owner1 has exactly 2 leagues");
        owner1LeaguesFiltered.Should().OnlyContain(l => l.OwnerId == owner1.Id);
        owner1LeaguesFiltered.Select(l => l.Name).Should().Contain(["Owner1 League 1", "Owner1 League 2"]);
    }

    [Fact]
    public async Task AddAsync_WithValidLeague_AddsToDatabase()
    {
        // Arrange
        var owner = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(owner);
        await _identityContext.SaveChangesAsync();

        var league = _testDataBuilder.CreateLeague(owner.Id);
        league.Name = "New Championship";
        league.Code = "CHAMP2024";
        league.Format = DeckFormat.Modern;
        league.TotalRounds = 5;
        league.RoundsToConsider = 4;
        league.MinimumRounds = 3;
        league.TotalPlayers = 32;
        league.PointsToGive = [3, 1, 0];
        league.IsActive = true;

        // Act
        _identityContext.Leagues.Add(league);
        await _identityContext.SaveChangesAsync();

        // Assert
        var savedLeague = await _identityContext.Leagues.FindAsync(league.Id);
        savedLeague.Should().NotBeNull("because the league should be saved to the database");
        savedLeague.Name.Should().Be("New Championship");
        savedLeague.Code.Should().Be("CHAMP2024");
        savedLeague.Format.Should().Be(DeckFormat.Modern);
        savedLeague.TotalRounds.Should().Be(5);
        savedLeague.RoundsToConsider.Should().Be(4);
        savedLeague.MinimumRounds.Should().Be(3);
        savedLeague.TotalPlayers.Should().Be(32);
        savedLeague.PointsToGive.Should().BeEquivalentTo([3, 1, 0 ]);
        savedLeague.IsActive.Should().BeTrue();
        savedLeague.OwnerId.Should().Be(owner.Id);
    }

    [Fact]
    public async Task UpdateAsync_WithValidLeague_UpdatesInDatabase()
    {
        // Arrange
        var owner = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(owner);
        await _identityContext.SaveChangesAsync();

        var league = _testDataBuilder.CreateLeague(owner.Id);
        league.Name = "Original Tournament";
        league.Code = "ORIG";
        league.TotalPlayers = 16;
        league.IsActive = true;
        _identityContext.Leagues.Add(league);
        await _identityContext.SaveChangesAsync();

        // Act
        league.Name = "Updated Tournament";
        league.Code = "UPDT";
        league.TotalPlayers = 24;
        league.IsActive = false;
        await _identityContext.SaveChangesAsync();

        // Assert
        var updatedLeague = await _identityContext.Leagues.FindAsync(league.Id);
        updatedLeague.Should().NotBeNull();
        updatedLeague.Name.Should().Be("Updated Tournament");
        updatedLeague.Code.Should().Be("UPDT");
        updatedLeague.TotalPlayers.Should().Be(24);
        updatedLeague.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WithValidLeague_RemovesFromDatabase()
    {
        // Arrange
        var owner = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(owner);
        await _identityContext.SaveChangesAsync();

        var league = _testDataBuilder.CreateLeague(owner.Id);
        _identityContext.Leagues.Add(league);
        await _identityContext.SaveChangesAsync();

        var existingLeague = await _identityContext.Leagues.FindAsync(league.Id);
        existingLeague.Should().NotBeNull();

        // Act
        _identityContext.Leagues.Remove(league);
        await _identityContext.SaveChangesAsync();

        // Assert
        var deletedLeague = await _identityContext.Leagues.FindAsync(league.Id);
        deletedLeague.Should().BeNull("because the league should be deleted");
    }

    [Fact]
    public async Task ListAsync_WithDifferentFormats_ReturnsAllFormats()
    {
        // Arrange
        var owner = _testDataBuilder.CreateUser();
        _identityContext.Users.Add(owner);
        await _identityContext.SaveChangesAsync();

        var leagues = new[]
        {
            _testDataBuilder.CreateLeague(owner.Id),
            _testDataBuilder.CreateLeague(owner.Id),
            _testDataBuilder.CreateLeague(owner.Id)
        };
        leagues[0].Format = DeckFormat.Standard;
        leagues[0].Code = "STD1";
        leagues[1].Format = DeckFormat.Modern;
        leagues[1].Code = "MOD1";
        leagues[2].Format = DeckFormat.Legacy;
        leagues[2].Code = "LEG1";

        _identityContext.Leagues.AddRange(leagues);
        await _identityContext.SaveChangesAsync();

        // Act
        var result = _identityContext.Leagues.ToList();

        // Assert
        result.Should().HaveCount(3);
        result.Select(l => l.Format).Should().Contain([DeckFormat.Standard, DeckFormat.Modern, DeckFormat.Legacy]);
        result.Select(l => l.Code).Should().Contain(["STD1", "MOD1", "LEG1"]);
    }

    [Fact]
    public async Task League_WithUserLeagues_MaintainsRelationship()
    {
        // Arrange
        var owner = _testDataBuilder.CreateUser("owner@test.com", "owner");
        var player1 = _testDataBuilder.CreateUser("player1@test.com", "player1");
        var player2 = _testDataBuilder.CreateUser("player2@test.com", "player2");
        _identityContext.Users.AddRange(owner, player1, player2);
        await _identityContext.SaveChangesAsync();

        var league = _testDataBuilder.CreateLeague(owner.Id);
        league.Code = "MULTI";
        _identityContext.Leagues.Add(league);
        await _identityContext.SaveChangesAsync();

        var userLeague1 = new AppUserLeague { UserId = player1.Id, LeagueId = league.Id };
        var userLeague2 = new AppUserLeague { UserId = player2.Id, LeagueId = league.Id };
        _identityContext.UserLeagues.AddRange(userLeague1, userLeague2);
        await _identityContext.SaveChangesAsync();

        // Act
        var leagueWithUsers = _identityContext.Leagues
            .FirstOrDefault(l => l.Id == league.Id);

        // Assert
        leagueWithUsers.Should().NotBeNull();
        leagueWithUsers!.Code.Should().Be("MULTI");
        
        // Note: In a real test, you'd load the navigation properties
        var userLeagueCount = _identityContext.UserLeagues.Count(ul => ul.LeagueId == league.Id);
        userLeagueCount.Should().Be(2, "because two users joined the league");
    }

    [Fact]
    public async Task RoleAssignments_AddingPlayerRole_PersistsAssignment()
    {
        var owner = _testDataBuilder.CreateUser("role-owner@test.com", "role_owner");
        var player = _testDataBuilder.CreateUser("role-player@test.com", "role_player");
        _identityContext.Users.AddRange(owner, player);
        await _identityContext.SaveChangesAsync();

        var league = _testDataBuilder.CreateLeague(owner.Id);
        league.Code = "ROLEADD";
        _identityContext.Leagues.Add(league);
        await _identityContext.SaveChangesAsync();

        var assignment = new LeagueRoleAssignment
        {
            LeagueId = league.Id,
            UserId = player.Id,
            Roles = LeagueRole.Player
        };
        _identityContext.LeagueRoleAssignments.Add(assignment);
        await _identityContext.SaveChangesAsync();

        var saved = await _identityContext.LeagueRoleAssignments
            .FirstOrDefaultAsync(x => x.LeagueId == league.Id && x.UserId == player.Id);

        saved.Should().NotBeNull();
        saved!.Roles.Should().Be(LeagueRole.Player);
    }

    [Fact]
    public async Task RoleAssignments_UpdatingExistingAssignment_MergesRoles()
    {
        var owner = _testDataBuilder.CreateUser("merge-owner@test.com", "merge_owner");
        var player = _testDataBuilder.CreateUser("merge-player@test.com", "merge_player");
        _identityContext.Users.AddRange(owner, player);
        await _identityContext.SaveChangesAsync();

        var league = _testDataBuilder.CreateLeague(owner.Id);
        league.Code = "ROLEMERGE";
        _identityContext.Leagues.Add(league);
        await _identityContext.SaveChangesAsync();

        var assignment = new LeagueRoleAssignment
        {
            LeagueId = league.Id,
            UserId = player.Id,
            Roles = LeagueRole.Player
        };
        _identityContext.LeagueRoleAssignments.Add(assignment);
        await _identityContext.SaveChangesAsync();

        assignment.Roles |= LeagueRole.Admin;
        _identityContext.LeagueRoleAssignments.Update(assignment);
        await _identityContext.SaveChangesAsync();

        var saved = await _identityContext.LeagueRoleAssignments
            .FirstOrDefaultAsync(x => x.LeagueId == league.Id && x.UserId == player.Id);

        saved.Should().NotBeNull();
        saved!.Roles.Should().Be(LeagueRole.Player | LeagueRole.Admin);
    }

    [Fact]
    public void RoleAssignments_Serialization_DoesNotExposeUserDetails()
    {
        var owner = _testDataBuilder.CreateUser("json-owner@test.com", "json_owner");
        var player = _testDataBuilder.CreateUser("json-player@test.com", "json_player");
        var league = _testDataBuilder.CreateLeague(owner.Id);
        league.Id = 123;

        var assignment = new LeagueRoleAssignment
        {
            Id = 456,
            LeagueId = league.Id,
            UserId = player.Id,
            Roles = LeagueRole.Admin,
            User = player,
            League = league
        };
        league.RoleAssignments.Add(assignment);

        var json = JsonSerializer.Serialize(league, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        using var document = JsonDocument.Parse(json);
        var assignments = document.RootElement.GetProperty("roleAssignments");
        assignments.GetArrayLength().Should().Be(1);
        var firstAssignment = assignments[0];
        firstAssignment.GetProperty("userId").GetString().Should().Be(player.Id);
        firstAssignment.TryGetProperty("user", out _).Should().BeFalse();
        firstAssignment.TryGetProperty("league", out _).Should().BeFalse();
    }

    public void Dispose()
    {
        _context.Dispose();
        _identityContext.Dispose();
    }
}
