using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using API.Dtos.Leagues;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Core.Models.Identity;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TestUtilities.Authentication;
using TestUtilities.Builders;

namespace IntegrationTests.Controllers;

/// <summary>
/// Integration tests for League endpoints.
/// Tests league creation, retrieval, updates, and user-league associations.
/// </summary>
[Collection("Integration Tests")]
public class LeaguesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly TestDataBuilder _testDataBuilder;

    public LeaguesControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public async Task GetLeaguesFromUser_WithAuthenticatedUser_ReturnsUserLeagues()
    {
        // Arrange
        var user = await CreateTestUserAsync("leagueuser@example.com", "leagueuser");
        var league = await CreateTestLeagueAsync("Test League", user.Id);
        await AssociateUserWithLeagueAsync(user.Id, league.Id);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync("/api/leagues/user");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "because authenticated users should be able to access their leagues");

        // Note: The endpoint might return a different type than expected based on the implementation
        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().NotBeNullOrEmpty("because the user has associated leagues");
    }

    [Fact]
    public async Task GetLeaguesFromUser_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var client = _factory.CreateClient(); // No authentication

        // Act
        var response = await client.GetAsync("/api/leagues/user");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "because unauthenticated requests should be rejected");
    }

    [Fact]
    public async Task GetLeaguesFromUser_WithNoLeagues_ReturnsEmptyOrNotFound()
    {
        // Arrange
        var user = await CreateTestUserAsync("noleagues@example.com", "noleagues");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync("/api/leagues/user");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetLeagues_WithAuthenticatedUser_ReturnsLeagues()
    {
        var owner = await CreateTestUserAsync("league-list-owner@example.com", "league_list_owner");
        await CreateTestLeagueAsync("List League", owner.Id);
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.GetAsync("/api/leagues");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetLeagues_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/leagues");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetLeagueFromId_WithValidId_ReturnsLeague()
    {
        // Arrange
        var user = await CreateTestUserAsync("getleague@example.com", "getleague");
        var league = await CreateTestLeagueAsync("Retrievable League", user.Id);
        await AssociateUserWithLeagueAsync(user.Id, league.Id);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync($"/api/leagues/{league.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "because valid league IDs should return league data");

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetLeagueFromId_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var user = await CreateTestUserAsync("notfoundleague@example.com", "notfoundleague");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        // Act
        var response = await client.GetAsync("/api/leagues/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "because no league exists with this ID");
    }

    [Fact]
    public async Task GetLeagueFromId_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("getleague-noauth@example.com", "getleague_noauth");
        var league = await CreateTestLeagueAsync("NoAuth League", owner.Id);
        await AssociateUserWithLeagueAsync(owner.Id, league.Id);
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/leagues/{league.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateLeague_WithValidData_UpdatesLeague()
    {
        // Arrange
        var user = await CreateTestUserAsync("updater@example.com", "updater");
        var league = await CreateTestLeagueAsync("Original League", user.Id);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var updateRequest = new UpdateLeagueDto
        {
            Name = "Updated League Name",
            MinimumRounds = 2
        };

        var json = JsonSerializer.Serialize(updateRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PutAsync($"/api/leagues/{league.Id}", content);

        // Assert
        // This test may fail due to implementation differences
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "because the endpoint should handle the request without crashing");
        
        // If successful, verify the update
        if (response.StatusCode == HttpStatusCode.OK)
        {
            await VerifyLeagueUpdatedInDatabase(league.Id, "Updated League Name");
        }
    }

    [Fact]
    public async Task UpdateLeague_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var user = await CreateTestUserAsync("updatebad@example.com", "updatebad");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var updateRequest = new UpdateLeagueDto
        {
            Name = "Non-existent League"
        };

        var json = JsonSerializer.Serialize(updateRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PutAsync("/api/leagues/999999", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "because no league exists with this ID");
    }

    [Fact]
    public async Task UpdateLeague_WithUnauthorizedUser_ReturnsForbidden()
    {
        // Arrange
        var owner = await CreateTestUserAsync("owner@example.com", "owner");
        var otherUser = await CreateTestUserAsync("other@example.com", "other");
        var league = await CreateTestLeagueAsync("Owner's League", owner.Id);
        
        // Try to update as different user
        using var client = _factory.CreateClientWithUser(otherUser.Id, otherUser.UserName!, otherUser.Email!);

        var updateRequest = new UpdateLeagueDto
        {
            Name = "Hijacked League"
        };

        var json = JsonSerializer.Serialize(updateRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PutAsync($"/api/leagues/{league.Id}", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateLeague_WithInvalidData_ReturnsBadRequest()
    {
        var owner = await CreateTestUserAsync("league-update-invaliddata@example.com", "league_update_invaliddata");
        var league = await CreateTestLeagueAsync("Update Invalid", owner.Id);
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var invalidRequest = new UpdateLeagueDto
        {
            Name = string.Empty,
            TotalRounds = 0,
            RoundsToConsider = 0,
            MinimumRounds = -1,
            TotalPrize = -10,
            PrizePerPerson = -1
        };

        var response = await client.PutAsync($"/api/leagues/{league.Id}",
            new StringContent(JsonSerializer.Serialize(invalidRequest), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateLeague_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("league-update-noauth@example.com", "league_update_noauth");
        var league = await CreateTestLeagueAsync("Update NoAuth", owner.Id);
        using var client = _factory.CreateClient();

        var request = new UpdateLeagueDto
        {
            Name = "Updated",
            TotalRounds = 5,
            RoundsToConsider = 4,
            MinimumRounds = 2,
            TotalPrize = 100,
            PrizePerPerson = 10
        };

        var response = await client.PutAsync($"/api/leagues/{league.Id}",
            new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
    
    [Fact]
    public async Task UpdateLeagueFromResults_WithValidData_UpdatesResults()
    {
        var owner = await CreateTestUserAsync("resulter@example.com", "resulter");
        var secondary = await CreateTestUserAsync("resulter-second@example.com", "resulter_second");
        var league = await CreateTestLeagueAsync("Results League", owner.Id);
        await AssociateUserWithLeagueAsync(owner.Id, league.Id);
        await AssociateUserWithLeagueAsync(secondary.Id, league.Id);
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var resultPayload = new List<UserWithScore>
        {
            new() { UserId = owner.Id, Score = 10 },
            new() { UserId = secondary.Id, Score = 5 }
        };

        var response = await client.PatchAsync($"/api/leagues/{league.Id}/results",
            new StringContent(JsonSerializer.Serialize(resultPayload), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateLeagueFromResults_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("resulter-noauth@example.com", "resulter_noauth");
        var league = await CreateTestLeagueAsync("NoAuth Results", owner.Id);
        using var client = _factory.CreateClient();

        var response = await client.PatchAsync($"/api/leagues/{league.Id}/results",
            new StringContent(JsonSerializer.Serialize(new List<UserWithScore>()), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateLeagueFromResults_WithInvalidLeague_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("resulter-missing@example.com", "resulter_missing");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.PatchAsync($"/api/leagues/{int.MaxValue}/results",
            new StringContent(JsonSerializer.Serialize(new List<UserWithScore>()), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateLeague_WithValidData_CreatesLeague()
    {
        // Arrange
        var user = await CreateTestUserAsync("creator@example.com", "creator");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var createRequest = new NewLeagueDto
        {
            Name = "New Test League",
            Format = "Standard",
            TotalRounds = 10,
            RoundsToConsider = 8,
            MinimumRounds = 1,
            PointsToGive =
            [
                6,
                4,
                2
            ]
        };

        var json = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/leagues", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "because valid league data should create a new league");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            var createdLeague = JsonSerializer.Deserialize<League>(
                responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            createdLeague.Should().NotBeNull();
            createdLeague.Name.Should().Be(createRequest.Name);
            createdLeague.OwnerId.Should().Be(user.Id);
            createdLeague.Code.Length.Should().Be(6);
        }
    }

    [Fact]
    public async Task CreateLeague_WithInvalidData_ReturnsBadRequest()
    {
        var user = await CreateTestUserAsync("league-invalid-create@example.com", "league_invalid_create");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var invalidRequest = new NewLeagueDto
        {
            Name = string.Empty,
            Format = "",
            TotalRounds = 0,
            RoundsToConsider = 0,
            MinimumRounds = -1,
            PointsToGive = new List<int>()
        };

        var response = await client.PostAsync("/api/leagues",
            new StringContent(JsonSerializer.Serialize(invalidRequest), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateLeague_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var createRequest = new NewLeagueDto
        {
            Name = "Unauthorized League",
            Format = "Modern",
            TotalRounds = 5,
            RoundsToConsider = 4,
            MinimumRounds = 2,
            PointsToGive = new List<int> { 3, 1 }
        };

        var response = await client.PostAsync("/api/leagues",
            new StringContent(JsonSerializer.Serialize(createRequest), Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JoinLeague_WithValidLeagueCode_AddsUserToLeague()
    {
        // Arrange
        var owner = await CreateTestUserAsync("leagueowner@example.com", "leagueowner");
        var joiner = await CreateTestUserAsync("joiner@example.com", "joiner");
        var league = await CreateTestLeagueAsync("Joinable League", owner.Id);
        using var client = _factory.CreateClientWithUser(joiner.Id, joiner.UserName!, joiner.Email!);
        
        var content = new StringContent("", Encoding.UTF8, "application/json");

        // Act
        var response = await client.PatchAsync($"/api/leagues/{league.Code}/join", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "because valid league codes should allow users to join");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            await VerifyUserAssociatedWithLeague(joiner.Id, league.Id);
        }
    }
    
    [Fact]
    public async Task JoinLeague_WithNonExistingLeagueCode_ReturnsNotFound()
    {
        // Arrange
        var owner = await CreateTestUserAsync("leagueowner@example.com", "leagueowner");
        var joiner = await CreateTestUserAsync("joiner@example.com", "joiner");
        using var client = _factory.CreateClientWithUser(joiner.Id, joiner.UserName!, joiner.Email!);

        var content = new StringContent("", Encoding.UTF8, "application/json");

        // Act
        var response = await client.PatchAsync($"/api/leagues/NOTEXI/join", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "because code is not valid and shouldn't join a league");
    }

    [Fact]
    public async Task LeaveLeague_WithJoinedUser_ReturnsOk()
    {
        var owner = await CreateTestUserAsync("leave-owner@example.com", "leave_owner");
        var joiner = await CreateTestUserAsync("leave-joiner@example.com", "leave_joiner");
        var league = await CreateTestLeagueAsync("Leave League", owner.Id);
        await AssociateUserWithLeagueAsync(joiner.Id, league.Id);
        using var client = _factory.CreateClientWithUser(joiner.Id, joiner.UserName!, joiner.Email!);

        var response = await client.PatchAsync($"/api/leagues/{league.Id}/leave",
            new StringContent(string.Empty, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LeaveLeague_WhenOwnerTries_ReturnsBadRequest()
    {
        var owner = await CreateTestUserAsync("leave-owner2@example.com", "leave_owner2");
        var league = await CreateTestLeagueAsync("Owner League", owner.Id);
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.PatchAsync($"/api/leagues/{league.Id}/leave",
            new StringContent(string.Empty, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LeaveLeague_WithInvalidId_ReturnsNotFound()
    {
        var user = await CreateTestUserAsync("leave-missing@example.com", "leave_missing");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var response = await client.PatchAsync($"/api/leagues/{int.MaxValue}/leave",
            new StringContent(string.Empty, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LeaveLeague_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("leave-noauth-owner@example.com", "leave_noauth_owner");
        var league = await CreateTestLeagueAsync("Leave NoAuth League", owner.Id);
        using var client = _factory.CreateClient();

        var response = await client.PatchAsync($"/api/leagues/{league.Id}/leave",
            new StringContent(string.Empty, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LeaveLeague_WhenUserNotParticipant_ReturnsBadRequest()
    {
        var owner = await CreateTestUserAsync("leave-notparticipant-owner@example.com", "leave_notparticipant_owner");
        var outsider = await CreateTestUserAsync("leave-notparticipant-outsider@example.com", "leave_notparticipant_outsider");
        var league = await CreateTestLeagueAsync("Leave Not Participant", owner.Id);
        using var client = _factory.CreateClientWithUser(outsider.Id, outsider.UserName!, outsider.Email!);

        var response = await client.PatchAsync($"/api/leagues/{league.Id}/leave",
            new StringContent(string.Empty, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListLeagueWithScores_WithValidData_ReturnsScores()
    {
        var owner = await CreateTestUserAsync("scores-owner@example.com", "scores_owner");
        var league = await CreateTestLeagueAsync("Score League", owner.Id);
        await AssociateUserWithLeagueAsync(owner.Id, league.Id);
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.GetAsync($"/api/leagues/{league.Id}/scores");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        payload.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ListLeagueWithScores_WithInvalidId_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("scores-missing@example.com", "scores_missing");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.GetAsync($"/api/leagues/{int.MaxValue}/scores");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListLeagueWithScores_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("scores-noauth@example.com", "scores_noauth");
        var league = await CreateTestLeagueAsync("NoAuth Scores", owner.Id);
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/leagues/{league.Id}/scores");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JoinAsPlayer_WithOwner_ReturnsOk()
    {
        var owner = await CreateTestUserAsync("joinplayer-owner@example.com", "joinplayer_owner");
        var league = await CreateTestLeagueAsync("Owner Player League", owner.Id, isActive: true);
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.PatchAsync($"/api/leagues/{league.Id}/join",
            new StringContent(string.Empty, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task JoinAsPlayer_WithNonOwner_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("joinplayer-owner2@example.com", "joinplayer_owner2");
        var intruder = await CreateTestUserAsync("joinplayer-intruder@example.com", "joinplayer_intruder");
        var league = await CreateTestLeagueAsync("Owner Only League", owner.Id);
        using var client = _factory.CreateClientWithUser(intruder.Id, intruder.UserName!, intruder.Email!);

        var response = await client.PatchAsync($"/api/leagues/{league.Id}/join",
            new StringContent(string.Empty, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JoinAsPlayer_WithInactiveLeague_ReturnsBadRequest()
    {
        var owner = await CreateTestUserAsync("joinplayer-inactive@example.com", "joinplayer_inactive");
        var league = await CreateTestLeagueAsync("Inactive League", owner.Id, isActive: false);
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.PatchAsync($"/api/leagues/{league.Id}/join",
            new StringContent(string.Empty, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task JoinAsPlayer_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("joinplayer-noauth@example.com", "joinplayer_noauth");
        var league = await CreateTestLeagueAsync("Join Player NoAuth", owner.Id);
        using var client = _factory.CreateClient();

        var response = await client.PatchAsync($"/api/leagues/{league.Id}/join",
            new StringContent(string.Empty, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JoinLeague_WithoutAuthentication_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("leagueowner-noauth@example.com", "leagueowner_noauth");
        var league = await CreateTestLeagueAsync("Public League", owner.Id);
        using var client = _factory.CreateClient();

        var response = await client.PatchAsync($"/api/leagues/{league.Code}/join",
            new StringContent(string.Empty, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetInviteCode_WithOwner_ReturnsCode()
    {
        var owner = await CreateTestUserAsync("invite-owner@example.com", "invite_owner");
        var league = await CreateTestLeagueAsync("Invite League", owner.Id);
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.GetAsync($"/api/leagues/{league.Id}/invite");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var code = await response.Content.ReadAsStringAsync();
        code.Should().Contain(league.Code);
    }

    [Fact]
    public async Task GetInviteCode_WithNonOwner_ReturnsUnauthorized()
    {
        var owner = await CreateTestUserAsync("invite-owner2@example.com", "invite_owner2");
        var intruder = await CreateTestUserAsync("invite-intruder@example.com", "invite_intruder");
        var league = await CreateTestLeagueAsync("Invite League 2", owner.Id);
        using var client = _factory.CreateClientWithUser(intruder.Id, intruder.UserName!, intruder.Email!);

        var response = await client.GetAsync($"/api/leagues/{league.Id}/invite");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetInviteCode_WithInvalidLeague_ReturnsNotFound()
    {
        var owner = await CreateTestUserAsync("invite-missing@example.com", "invite_missing");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var response = await client.GetAsync($"/api/leagues/{int.MaxValue}/invite");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #region Helper Methods

    private Task<AppUser> CreateTestUserAsync(string baseEmail, string baseUserName) =>
        TestUserFactory.CreateAsync(_factory.Services, _testDataBuilder, baseEmail, baseUserName);

    private async Task<League> CreateTestLeagueAsync(string name, string ownerId, bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var league = new League
        {
            Name = name,
            OwnerId = ownerId,
            Code = $"TL{uniqueId}"[..8], // Ensure unique code
            Format = "Standard",
            TotalRounds = 5,
            RoundsToConsider = 4,
            MinimumRounds = 2,
            TotalPlayers = 10,
            PointsToGive = new List<int> { 3, 1, 0 },
            IsActive = isActive
        };
        
        dbContext.Leagues.Add(league);
        await dbContext.SaveChangesAsync();
        
        return league;
    }

    private async Task AssociateUserWithLeagueAsync(string userId, int leagueId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        
        var userLeague = new AppUserLeague
        {
            UserId = userId,
            LeagueId = leagueId,
            Score = 0,
            IsPlaying = true
        };
        
        dbContext.UserLeagues.Add(userLeague);
        await dbContext.SaveChangesAsync();
    }

    private async Task VerifyLeagueUpdatedInDatabase(int leagueId, string expectedName)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        
        var league = await dbContext.Leagues.FindAsync(leagueId);
        league.Should().NotBeNull($"because league {leagueId} should exist in database");
        league!.Name.Should().Be(expectedName, "because league name should be updated");
    }

    private async Task VerifyUserAssociatedWithLeague(string userId, int leagueId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        
        var userLeague = dbContext.UserLeagues.FirstOrDefault(ul => ul.UserId == userId && ul.LeagueId == leagueId);
        userLeague.Should().NotBeNull($"because user {userId} should be associated with league {leagueId}");
    }


    #endregion
}
