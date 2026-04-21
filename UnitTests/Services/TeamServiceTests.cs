using API.Dtos.Teams;
using API.Services;
using Core.Enums;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace UnitTests.Services;

public class TeamServiceTests
{
    private static MainContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MainContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new MainContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static IUnitOfWork CreateUow(MainContext context)
        => new UnitOfWork(context, NullLogger<UnitOfWork>.Instance, NullLoggerFactory.Instance);

    private static TeamService CreateService(MainContext context, params AppUser[] users)
    {
        var uow = CreateUow(context);
        var manager = CreateUserManagerMock(users);
        var notificationService = new NotificationService(uow, NullLogger<NotificationService>.Instance);
        return new TeamService(uow, manager.Object, notificationService);
    }

    private static Mock<UserManager<AppUser>> CreateUserManagerMock(params AppUser[] users)
    {
        var store = new Mock<IUserStore<AppUser>>();
        var manager = new Mock<UserManager<AppUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        foreach (var user in users)
            manager.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        return manager;
    }

    private static AppUser CreateUser(string id)
        => new()
        {
            Id = id,
            DisplayName = id,
            FirstName = id,
            LastName = "User",
            UserName = id,
            Email = $"{id}@example.com"
        };

    [Fact]
    public async Task CreateTeam_ValidInput_CreatesTeamAndOwnerMember()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        context.Users.Add(owner);
        await context.SaveChangesAsync();

        var service = CreateService(context, owner);

        var result = await service.CreateTeamAsync(new CreateTeamDto { Name = "Test Team", Description = "Desc" }, owner.Id);

        result.Name.Should().Be("Test Team");
        result.OwnerId.Should().Be(owner.Id);
        result.MemberCount.Should().Be(1);

        var team = await context.Teams.SingleAsync();
        team.Name.Should().Be("Test Team");

        var member = await context.Set<TeamMember>().SingleAsync();
        member.UserId.Should().Be(owner.Id);
        member.Role.Should().Be(TeamRole.Owner);
    }

    [Fact]
    public async Task InviteMember_ByOwner_CreatesPendingMember()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var invitee = CreateUser("invitee");
        context.Users.AddRange(owner, invitee);
        await context.SaveChangesAsync();

        var service = CreateService(context, owner, invitee);
        var team = await service.CreateTeamAsync(new CreateTeamDto { Name = "Team" }, owner.Id);

        await service.InviteMemberAsync(team.Id, owner.Id, invitee.Id);

        var members = await context.Set<TeamMember>().ToListAsync();
        members.Should().HaveCount(2);

        var invited = members.Single(m => m.UserId == invitee.Id);
        invited.Role.Should().Be(TeamRole.Member);
        invited.InvitedById.Should().Be(owner.Id);
    }

    [Fact]
    public async Task InviteMember_ByMember_ThrowsForbidden()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var member = CreateUser("member");
        var invitee = CreateUser("invitee");
        context.Users.AddRange(owner, member, invitee);
        await context.SaveChangesAsync();

        var service = CreateService(context, owner, member, invitee);
        var team = await service.CreateTeamAsync(new CreateTeamDto { Name = "Team" }, owner.Id);
        await service.InviteMemberAsync(team.Id, owner.Id, member.Id);

        var act = () => service.InviteMemberAsync(team.Id, member.Id, invitee.Id);

        await act.Should().ThrowAsync<TeamServiceException>()
            .Where(ex => ex.StatusCode == 403);
    }

    [Fact]
    public async Task AcceptInvite_InvitedMember_CompletesSuccessfully()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var invitee = CreateUser("invitee");
        context.Users.AddRange(owner, invitee);
        await context.SaveChangesAsync();

        var service = CreateService(context, owner, invitee);
        var team = await service.CreateTeamAsync(new CreateTeamDto { Name = "Team" }, owner.Id);
        await service.InviteMemberAsync(team.Id, owner.Id, invitee.Id);

        var act = () => service.AcceptInviteAsync(team.Id, invitee.Id);

        await act.Should().NotThrowAsync();
        (await service.HasTeamAccessAsync(team.Id, invitee.Id, TeamRole.Member)).Should().BeTrue();
    }

    [Fact]
    public async Task RemoveMember_Owner_ThrowsForbidden()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var admin = CreateUser("admin");
        context.Users.AddRange(owner, admin);
        await context.SaveChangesAsync();

        var service = CreateService(context, owner, admin);
        var team = await service.CreateTeamAsync(new CreateTeamDto { Name = "Team" }, owner.Id);
        await service.InviteMemberAsync(team.Id, owner.Id, admin.Id);
        await service.UpdateMemberRoleAsync(team.Id, owner.Id, admin.Id, TeamRole.Admin);

        var act = () => service.RemoveMemberAsync(team.Id, admin.Id, owner.Id);

        await act.Should().ThrowAsync<TeamServiceException>()
            .Where(ex => ex.StatusCode == 403);
    }

    [Fact]
    public async Task DeleteTeam_ByOwner_Succeeds()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        context.Users.Add(owner);
        await context.SaveChangesAsync();

        var service = CreateService(context, owner);
        var team = await service.CreateTeamAsync(new CreateTeamDto { Name = "Team" }, owner.Id);

        await service.DeleteTeamAsync(team.Id, owner.Id);

        context.Teams.Should().BeEmpty();
        context.Set<TeamMember>().Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteTeam_ByAdmin_ThrowsForbidden()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var admin = CreateUser("admin");
        context.Users.AddRange(owner, admin);
        await context.SaveChangesAsync();

        var service = CreateService(context, owner, admin);
        var team = await service.CreateTeamAsync(new CreateTeamDto { Name = "Team" }, owner.Id);
        await service.InviteMemberAsync(team.Id, owner.Id, admin.Id);
        await service.UpdateMemberRoleAsync(team.Id, owner.Id, admin.Id, TeamRole.Admin);

        var act = () => service.DeleteTeamAsync(team.Id, admin.Id);

        await act.Should().ThrowAsync<TeamServiceException>()
            .Where(ex => ex.StatusCode == 403);
    }

    [Fact]
    public async Task HasTeamAccess_Owner_ReturnsTrueForAllRoles()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        context.Users.Add(owner);
        await context.SaveChangesAsync();

        var service = CreateService(context, owner);
        var team = await service.CreateTeamAsync(new CreateTeamDto { Name = "Team" }, owner.Id);

        (await service.HasTeamAccessAsync(team.Id, owner.Id, TeamRole.Member)).Should().BeTrue();
        (await service.HasTeamAccessAsync(team.Id, owner.Id, TeamRole.Admin)).Should().BeTrue();
        (await service.HasTeamAccessAsync(team.Id, owner.Id, TeamRole.Owner)).Should().BeTrue();
    }

    [Fact]
    public async Task HasTeamAccess_Member_ReturnsFalseForAdminRole()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var member = CreateUser("member");
        context.Users.AddRange(owner, member);
        await context.SaveChangesAsync();

        var service = CreateService(context, owner, member);
        var team = await service.CreateTeamAsync(new CreateTeamDto { Name = "Team" }, owner.Id);
        await service.InviteMemberAsync(team.Id, owner.Id, member.Id);

        (await service.HasTeamAccessAsync(team.Id, member.Id, TeamRole.Member)).Should().BeTrue();
        (await service.HasTeamAccessAsync(team.Id, member.Id, TeamRole.Admin)).Should().BeFalse();
        (await service.HasTeamAccessAsync(team.Id, member.Id, TeamRole.Owner)).Should().BeFalse();
    }
}
