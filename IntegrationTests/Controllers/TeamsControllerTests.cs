using System.Net;
using System.Text.Json;
using API.Dtos.Decks;
using API.Dtos.Teams;
using Core.Enums;
using Core.Models;
using Core.Models.Identity;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using TestUtilities.Authentication;
using TestUtilities.Builders;
using TestUtilities.Serialization;

namespace IntegrationTests.Controllers;

[Collection("Integration Tests")]
public class TeamsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly TestDataBuilder _testDataBuilder;

    public TeamsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public async Task CreateTeam_WithValidData_Returns201WithTeamDto()
    {
        var user = await CreateTestUserAsync("team-create@test.com", "team_creator");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var request = new CreateTeamDto { Name = "My Team", Description = "A great team" };
        var response = await client.PostAsync("/api/teams", Serialize(request));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadAsStringAsync();
        var team = JsonSerializer.Deserialize<TeamDto>(payload, JsonContentHelper.DefaultOptions);
        team.Should().NotBeNull();
        team!.Name.Should().Be("My Team");
        team.OwnerId.Should().Be(user.Id);
        team.MemberCount.Should().Be(1);
    }

    [Fact]
    public async Task GetTeams_ReturnsOnlyTeamsUserBelongsTo()
    {
        var owner = await CreateTestUserAsync("team-owner@test.com", "team_owner");
        var other = await CreateTestUserAsync("team-other@test.com", "team_other");

        using var ownerClient = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);
        using var otherClient = _factory.CreateClientWithUser(other.Id, other.UserName!, other.Email!);

        await ownerClient.PostAsync("/api/teams", Serialize(new CreateTeamDto { Name = "Owner Team" }));
        await otherClient.PostAsync("/api/teams", Serialize(new CreateTeamDto { Name = "Other Team" }));

        var response = await ownerClient.GetAsync("/api/teams");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var teams = JsonSerializer.Deserialize<List<TeamDto>>(
            await response.Content.ReadAsStringAsync(), JsonContentHelper.DefaultOptions);

        teams.Should().NotBeNull();
        teams!.Should().HaveCount(1);
        teams.Should().OnlyContain(t => t.OwnerId == owner.Id);
    }

    [Fact]
    public async Task GetTeamById_AsMember_Returns200()
    {
        var owner = await CreateTestUserAsync("team-get-owner@test.com", "team_get_owner");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var createResponse = await client.PostAsync("/api/teams", Serialize(new CreateTeamDto { Name = "Get Team" }));
        var created = JsonSerializer.Deserialize<TeamDto>(
            await createResponse.Content.ReadAsStringAsync(), JsonContentHelper.DefaultOptions);

        var response = await client.GetAsync($"/api/teams/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var team = JsonSerializer.Deserialize<TeamDto>(
            await response.Content.ReadAsStringAsync(), JsonContentHelper.DefaultOptions);
        team!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetTeamById_AsNonMember_Returns403()
    {
        var owner = await CreateTestUserAsync("team-get-nm-owner@test.com", "team_get_nm_owner");
        var stranger = await CreateTestUserAsync("team-get-nm-stranger@test.com", "team_get_nm_stranger");

        using var ownerClient = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);
        using var strangerClient = _factory.CreateClientWithUser(stranger.Id, stranger.UserName!, stranger.Email!);

        var createResponse = await ownerClient.PostAsync("/api/teams", Serialize(new CreateTeamDto { Name = "Private Team" }));
        var created = JsonSerializer.Deserialize<TeamDto>(
            await createResponse.Content.ReadAsStringAsync(), JsonContentHelper.DefaultOptions);

        var response = await strangerClient.GetAsync($"/api/teams/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteTeam_ByOwner_Returns204()
    {
        var owner = await CreateTestUserAsync("team-del-owner@test.com", "team_del_owner");
        using var client = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);

        var createResponse = await client.PostAsync("/api/teams", Serialize(new CreateTeamDto { Name = "Delete Me" }));
        var created = JsonSerializer.Deserialize<TeamDto>(
            await createResponse.Content.ReadAsStringAsync(), JsonContentHelper.DefaultOptions);

        var response = await client.DeleteAsync($"/api/teams/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"/api/teams/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTeam_ByMember_Returns403()
    {
        var owner = await CreateTestUserAsync("team-del-own2@test.com", "team_del_own2");
        var member = await CreateTestUserAsync("team-del-mem@test.com", "team_del_mem");

        using var ownerClient = _factory.CreateClientWithUser(owner.Id, owner.UserName!, owner.Email!);
        using var memberClient = _factory.CreateClientWithUser(member.Id, member.UserName!, member.Email!);

        var createResponse = await ownerClient.PostAsync("/api/teams", Serialize(new CreateTeamDto { Name = "Team" }));
        var created = JsonSerializer.Deserialize<TeamDto>(
            await createResponse.Content.ReadAsStringAsync(), JsonContentHelper.DefaultOptions);

        await ownerClient.PostAsync(
            $"/api/teams/{created!.Id}/members",
            Serialize(new InviteMemberDto { TargetUserId = member.Id }));

        var response = await memberClient.DeleteAsync($"/api/teams/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateDeck_WithoutTeamId_StillReturnsCreated()
    {
        var user = await CreateTestUserAsync("team-deck-create@test.com", "team_deck_create");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        await SeedCollectionForUserAsync(user.Id);

        var request = new CreateDeckDto { Name = "Solo Deck", Format = DeckFormat.Modern, IsPublic = false };
        var response = await client.PostAsync("/api/decks", Serialize(request));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        var deck = JsonSerializer.Deserialize<DeckDto>(payload, JsonContentHelper.DefaultOptions);
        deck!.OwnerId.Should().Be(user.Id);
        deck.TeamId.Should().BeNull();
    }

    private Task<AppUser> CreateTestUserAsync(string email, string userName) =>
        TestUserFactory.CreateAsync(_factory.Services, _testDataBuilder, email, userName);

    private async Task SeedCollectionForUserAsync(string userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MainContext>();
        context.Collections.Add(_testDataBuilder.CreateCollection(userId));
        await context.SaveChangesAsync();
    }

    private static StringContent Serialize<T>(T value) => JsonContentHelper.CreateContent(value);
}
