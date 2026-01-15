using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Dtos.Leagues;
using API.Services;
using Core.Enums;
using Core.Models.Identity;
using FluentAssertions;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace UnitTests.Services;

public class LeagueServiceTests
{
    [Fact]
    public async Task UpdateLeagueResultsAsync_PositionalUpdatesScoresAndRounds()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var playerOne = CreateUser("player-1");
        var playerTwo = CreateUser("player-2");
        context.Users.AddRange(owner, playerOne, playerTwo);

        var league = new League
        {
            Name = "League",
            OwnerId = owner.Id,
            Code = "ABC123",
            Format = "Modern",
            TotalRounds = 3,
            CurrentRound = 0,
            RoundsToConsider = 3,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 5,
            TotalPlayers = 2,
            PointsToGive = new List<int> { 3, 1 },
            ScoringSystem = ScoringSystem.Positional,
            IsActive = true,
            IsPublic = true
        };

        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        var rounds = new List<Round>
        {
            new() { League = league, LeagueId = league.Id, Order = 1 },
            new() { League = league, LeagueId = league.Id, Order = 2 },
            new() { League = league, LeagueId = league.Id, Order = 3 }
        };
        context.Rounds.AddRange(rounds);
        await context.SaveChangesAsync();

        league.CurrentRound = rounds[0].Id;
        context.Leagues.Update(league);
        await context.SaveChangesAsync();

        context.UserLeagues.AddRange(
            new AppUserLeague
            {
                UserId = playerOne.Id,
                User = playerOne,
                League = league,
                LeagueId = league.Id,
                Score = 0,
                RoundsPlayed = 0,
                BestRound = 0,
                AvgPosition = 0,
                IsPlaying = true
            },
            new AppUserLeague
            {
                UserId = playerTwo.Id,
                User = playerTwo,
                League = league,
                LeagueId = league.Id,
                Score = 0,
                RoundsPlayed = 0,
                BestRound = 0,
                AvgPosition = 0,
                IsPlaying = true
            });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, playerOne, playerTwo);

        await service.UpdateLeagueResultsAsync(
            league.Id,
            owner.Id,
            new List<UserWithScore>
            {
                new() { UserId = playerOne.Id },
                new() { UserId = playerTwo.Id }
            });

        var updatedLeague = await context.Leagues.SingleAsync();
        updatedLeague.TotalPrize.Should().Be(10);
        updatedLeague.CurrentRound.Should().Be(rounds[1].Id);

        var updatedUserLeagues = await context.UserLeagues
            .OrderBy(ul => ul.UserId)
            .ToListAsync();

        updatedUserLeagues[0].Score.Should().Be(3);
        updatedUserLeagues[0].RoundsPlayed.Should().Be(1);
        updatedUserLeagues[0].BestRound.Should().Be(1);
        updatedUserLeagues[0].Rounds.Should().BeEquivalentTo(new[] { 1 });
        updatedUserLeagues[0].AvgPosition.Should().Be(1);

        updatedUserLeagues[1].Score.Should().Be(1);
        updatedUserLeagues[1].RoundsPlayed.Should().Be(1);
        updatedUserLeagues[1].BestRound.Should().Be(2);
        updatedUserLeagues[1].Rounds.Should().BeEquivalentTo(new[] { 2 });
        updatedUserLeagues[1].AvgPosition.Should().Be(2);

        var round = await context.Rounds.SingleAsync(r => r.Order == 1);
        round.Status.Should().Be(Status.Played);

        var userRounds = await context.UserRounds
            .OrderBy(ur => ur.Position)
            .ToListAsync();
        userRounds.Should().HaveCount(2);
        userRounds[0].UserId.Should().Be(playerOne.Id);
        userRounds[0].Position.Should().Be(1);
        userRounds[0].Score.Should().Be(3);
        userRounds[1].UserId.Should().Be(playerTwo.Id);
        userRounds[1].Position.Should().Be(2);
        userRounds[1].Score.Should().Be(1);
    }

    [Fact]
    public async Task UpdateLeagueResultsAsync_VictoriesUsesWinLossPoints()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var player = CreateUser("player");
        context.Users.AddRange(owner, player);

        var league = new League
        {
            Name = "Victories League",
            OwnerId = owner.Id,
            Code = "VICT01",
            Format = "Commander",
            TotalRounds = 2,
            CurrentRound = 0,
            RoundsToConsider = 2,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 2,
            TotalPlayers = 1,
            ScoringSystem = ScoringSystem.Victories,
            PointsPerWin = 3,
            PointsPerDraw = 1,
            PointsPerLoss = 0,
            IsActive = true,
            IsPublic = true
        };

        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        var rounds = new List<Round>
        {
            new() { League = league, LeagueId = league.Id, Order = 1 },
            new() { League = league, LeagueId = league.Id, Order = 2 }
        };
        context.Rounds.AddRange(rounds);
        await context.SaveChangesAsync();

        league.CurrentRound = rounds[0].Id;
        context.Leagues.Update(league);
        await context.SaveChangesAsync();

        context.UserLeagues.Add(new AppUserLeague
        {
            UserId = player.Id,
            User = player,
            League = league,
            LeagueId = league.Id,
            Score = 0,
            RoundsPlayed = 0,
            BestRound = 0,
            AvgPosition = 0,
            IsPlaying = true
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, player);

        await service.UpdateLeagueResultsAsync(
            league.Id,
            owner.Id,
            new List<UserWithScore>
            {
                new()
                {
                    UserId = player.Id,
                    Wins = 2,
                    Draws = 1,
                    Losses = 0
                }
            });

        var updatedLeague = await context.Leagues.SingleAsync();
        updatedLeague.TotalPrize.Should().Be(2);
        updatedLeague.CurrentRound.Should().Be(rounds[1].Id);

        var updatedUserLeague = await context.UserLeagues.SingleAsync();
        updatedUserLeague.Score.Should().Be(7);
        updatedUserLeague.RoundsPlayed.Should().Be(1);
        updatedUserLeague.BestRound.Should().Be(1);
        updatedUserLeague.Rounds.Should().BeEquivalentTo(new[] { 1 });
        updatedUserLeague.AvgPosition.Should().Be(1);

        var round = await context.Rounds.SingleAsync(r => r.Order == 1);
        round.Status.Should().Be(Status.Played);

        var userRound = await context.UserRounds.SingleAsync();
        userRound.UserId.Should().Be(player.Id);
        userRound.Position.Should().Be(1);
        userRound.Score.Should().Be(7);
    }

    [Fact]
    public async Task UpdateLeagueResultsAsync_VictoriesUsesTiebreakersForPositions()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var playerOne = CreateUser("player-1");
        var playerTwo = CreateUser("player-2");
        context.Users.AddRange(owner, playerOne, playerTwo);

        var league = new League
        {
            Name = "Tiebreaker League",
            OwnerId = owner.Id,
            Code = "TIE01",
            Format = "Commander",
            TotalRounds = 2,
            CurrentRound = 0,
            RoundsToConsider = 2,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 2,
            TotalPlayers = 2,
            ScoringSystem = ScoringSystem.Victories,
            PointsPerWin = 3,
            PointsPerDraw = 1,
            PointsPerLoss = 0,
            IsActive = true,
            IsPublic = true
        };

        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        var rounds = new List<Round>
        {
            new() { League = league, LeagueId = league.Id, Order = 1 },
            new() { League = league, LeagueId = league.Id, Order = 2 }
        };
        context.Rounds.AddRange(rounds);
        await context.SaveChangesAsync();

        league.CurrentRound = rounds[0].Id;
        context.Leagues.Update(league);
        await context.SaveChangesAsync();

        context.UserLeagues.AddRange(
            new AppUserLeague
            {
                UserId = playerOne.Id,
                User = playerOne,
                League = league,
                LeagueId = league.Id,
                Score = 0,
                RoundsPlayed = 0,
                BestRound = 0,
                AvgPosition = 0,
                IsPlaying = true
            },
            new AppUserLeague
            {
                UserId = playerTwo.Id,
                User = playerTwo,
                League = league,
                LeagueId = league.Id,
                Score = 0,
                RoundsPlayed = 0,
                BestRound = 0,
                AvgPosition = 0,
                IsPlaying = true
            });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, playerOne, playerTwo);

        await service.UpdateLeagueResultsAsync(
            league.Id,
            owner.Id,
            new List<UserWithScore>
            {
                new()
                {
                    UserId = playerOne.Id,
                    Wins = 1,
                    Draws = 0,
                    Losses = 0,
                    Omw = 10,
                    Gw = 20,
                    Ogw = 30
                },
                new()
                {
                    UserId = playerTwo.Id,
                    Wins = 1,
                    Draws = 0,
                    Losses = 0,
                    Omw = 15,
                    Gw = 10,
                    Ogw = 5
                }
            });

        var userRounds = await context.UserRounds
            .OrderBy(ur => ur.Position)
            .ToListAsync();

        userRounds.Should().HaveCount(2);
        userRounds[0].UserId.Should().Be(playerTwo.Id);
        userRounds[0].Position.Should().Be(1);
        userRounds[0].Omw.Should().Be(15);
        userRounds[1].UserId.Should().Be(playerOne.Id);
        userRounds[1].Position.Should().Be(2);
        userRounds[1].Omw.Should().Be(10);
    }

    [Fact]
    public async Task UpdateLeagueResultsAsync_WithNonMemberPlayer_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var player = CreateUser("player");
        var outsider = CreateUser("outsider");
        context.Users.AddRange(owner, player, outsider);

        var league = new League
        {
            Name = "League",
            OwnerId = owner.Id,
            Code = "ABC123",
            Format = "Modern",
            TotalRounds = 1,
            CurrentRound = 0,
            RoundsToConsider = 1,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 5,
            TotalPlayers = 1,
            PointsToGive = new List<int> { 3, 1 },
            ScoringSystem = ScoringSystem.Positional,
            IsActive = true,
            IsPublic = true
        };

        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        var rounds = new List<Round>
        {
            new() { League = league, LeagueId = league.Id, Order = 1 }
        };
        context.Rounds.AddRange(rounds);
        await context.SaveChangesAsync();

        league.CurrentRound = rounds[0].Id;
        context.Leagues.Update(league);
        await context.SaveChangesAsync();

        context.UserLeagues.Add(new AppUserLeague
        {
            UserId = player.Id,
            User = player,
            League = league,
            LeagueId = league.Id,
            Score = 0,
            RoundsPlayed = 0,
            BestRound = 0,
            AvgPosition = 0,
            IsPlaying = true
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, owner, player, outsider);

        Func<Task> act = () => service.UpdateLeagueResultsAsync(
            league.Id,
            owner.Id,
            new List<UserWithScore>
            {
                new() { UserId = player.Id },
                new() { UserId = outsider.Id }
            });

        var exception = await act.Should().ThrowAsync<LeagueServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        exception.Which.Message.Should().Contain("Errors.Leagues.UserMustBeMember");
    }

    [Fact]
    public async Task JoinLeagueAsync_RequiresApprovedNotification()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var player = CreateUser("player");
        context.Users.AddRange(owner, player);

        var league = new League
        {
            Name = "Approval League",
            OwnerId = owner.Id,
            Code = "JOIN01",
            Format = "Legacy",
            TotalRounds = 1,
            CurrentRound = 0,
            RoundsToConsider = 1,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 1,
            TotalPlayers = 0,
            ScoringSystem = ScoringSystem.Victories,
            IsActive = true,
            IsPublic = true
        };
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        var service = CreateService(context, owner, player);

        Func<Task> act = () => service.JoinLeagueAsync(league.Id, player.Id);

        var exception = await act.Should().ThrowAsync<LeagueServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        exception.Which.Message.Should().Contain("Errors.Leagues.UserNotApproved");
        exception.Which.IncludeBody.Should().BeTrue();
        (await context.UserLeagues.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RequestJoinLeagueAsync_NotifiesAdminsAndOwner()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var admin = CreateUser("admin");
        var requester = CreateUser("requester");
        context.Users.AddRange(owner, admin, requester);

        var league = new League
        {
            Name = "Join League",
            OwnerId = owner.Id,
            Code = "REQ01",
            Format = "Standard",
            TotalRounds = 1,
            CurrentRound = 0,
            RoundsToConsider = 1,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 1,
            TotalPlayers = 0,
            ScoringSystem = ScoringSystem.Victories,
            IsActive = true,
            IsPublic = true
        };
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        context.LeagueRoleAssignments.Add(new LeagueRoleAssignment
        {
            League = league,
            LeagueId = league.Id,
            UserId = admin.Id,
            User = admin,
            Roles = LeagueRole.Admin
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, owner, admin, requester);

        await service.RequestJoinLeagueAsync(league.Code, requester.Id);

        var notifications = await context.Notifications.ToListAsync();
        notifications.Should().HaveCount(2);
        notifications.Select(n => n.AppUserId).Should().BeEquivalentTo(new[] { owner.Id, admin.Id });
        notifications.Should().OnlyContain(n => n.Name == "request_join_league");
        notifications.Should().OnlyContain(n => n.Origin == $"{league.Id}.{requester.Id}");
    }

    private static AppIdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AppIdentityDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static LeagueService CreateService(AppIdentityDbContext context, params AppUser[] users)
    {
        var manager = CreateUserManagerMock(users);
        var validationService = new ValidationService();
        var notificationService = new NotificationService(context, NullLogger<NotificationService>.Instance);
        return new LeagueService(manager.Object, context, validationService, notificationService);
    }

    private static Mock<UserManager<AppUser>> CreateUserManagerMock(params AppUser[] users)
    {
        var store = new Mock<IUserStore<AppUser>>();
        var manager = new Mock<UserManager<AppUser>>(store.Object, null, null, null, null, null, null, null, null);
        foreach (var user in users)
        {
            manager.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        }
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
}
