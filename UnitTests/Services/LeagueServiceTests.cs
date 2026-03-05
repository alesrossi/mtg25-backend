using API.Dtos.Leagues;
using API.Services;
using Core.Enums;
using Core.Interfaces;
using Core.Models.Identity;
using FluentAssertions;
using Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

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
            Format = DeckFormat.Modern,
            TotalRounds = 3,
            CurrentRound = 0,
            RoundsToConsider = 3,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 5,
            TotalPlayers = 2,
            PointsToGive = [3, 1],
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
        updatedUserLeagues[0].Rounds.Should().BeEquivalentTo([1]);
        updatedUserLeagues[0].AvgPosition.Should().Be(1);

        updatedUserLeagues[1].Score.Should().Be(1);
        updatedUserLeagues[1].RoundsPlayed.Should().Be(1);
        updatedUserLeagues[1].BestRound.Should().Be(2);
        updatedUserLeagues[1].Rounds.Should().BeEquivalentTo([2]);
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
            Format = DeckFormat.Commander,
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
        updatedUserLeague.Rounds.Should().BeEquivalentTo([1]);
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
            Format = DeckFormat.Commander,
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
            [
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
            ]);

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
            Format = DeckFormat.Modern,
            TotalRounds = 1,
            CurrentRound = 0,
            RoundsToConsider = 1,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 5,
            TotalPlayers = 1,
            PointsToGive = [3, 1],
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

        var act = () => service.UpdateLeagueResultsAsync(
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
            Format = DeckFormat.Legacy,
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

        var act = () => service.JoinLeagueAsync(league.Id, owner.Id, player.Id);

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
            Format = DeckFormat.Standard,
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

    [Fact]
    public async Task GetLeaguesForUserAsync_WithValidCurrentRound_PopulatesCurrentRoundOrder()
    {
        // Arrange
        await using var context = CreateContext();
        var user = CreateUser("user");
        var owner = CreateUser("owner");
        context.Users.AddRange(user, owner);

        var league = new League
        {
            Name = "Test League",
            OwnerId = owner.Id,
            Code = "TEST01",
            Format = DeckFormat.Standard,
            TotalRounds = 3,
            CurrentRound = 0,
            RoundsToConsider = 3,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 5,
            TotalPlayers = 1,
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

        league.CurrentRound = rounds[1].Id; // Set to second round (Order = 2)
        context.Leagues.Update(league);
        await context.SaveChangesAsync();

        context.UserLeagues.Add(new AppUserLeague
        {
            UserId = user.Id,
            User = user,
            League = league,
            LeagueId = league.Id,
            Score = 10,
            RoundsPlayed = 1,
            BestRound = 1,
            AvgPosition = 1,
            IsPlaying = true
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, user, owner);

        // Act
        var result = await service.GetLeaguesForUserAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Leagues.Should().HaveCount(1);
        
        var returnedLeague = result.Leagues[0];
        returnedLeague.Id.Should().Be(league.Id);
        returnedLeague.CurrentRound.Should().Be(rounds[1].Id);
        returnedLeague.CurrentRoundOrder.Should().Be(2, "because the current round has Order = 2");
    }

    [Fact]
    public async Task GetLeaguesForUserAsync_WithCurrentRoundZero_SetsCurrentRoundOrderToZero()
    {
        // Arrange
        await using var context = CreateContext();
        var user = CreateUser("user");
        var owner = CreateUser("owner");
        context.Users.AddRange(user, owner);

        var league = new League
        {
            Name = "Zero Round League",
            OwnerId = owner.Id,
            Code = "ZERO01",
            Format = DeckFormat.Standard,
            TotalRounds = 3,
            CurrentRound = 0,
            RoundsToConsider = 3,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 5,
            TotalPlayers = 1,
            ScoringSystem = ScoringSystem.Positional,
            IsActive = true,
            IsPublic = true
        };

        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        context.UserLeagues.Add(new AppUserLeague
        {
            UserId = user.Id,
            User = user,
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

        var service = CreateService(context, user, owner);

        // Act
        var result = await service.GetLeaguesForUserAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Leagues.Should().HaveCount(1);
        
        var returnedLeague = result.Leagues[0];
        returnedLeague.CurrentRound.Should().Be(0);
        returnedLeague.CurrentRoundOrder.Should().Be(0, "because CurrentRoundOrder should be 0 when CurrentRound is 0");
    }

    [Fact]
    public async Task GetLeaguesForUserAsync_WithInvalidCurrentRound_ThrowsBadRequest()
    {
        // Arrange
        await using var context = CreateContext();
        var user = CreateUser("user");
        var owner = CreateUser("owner");
        context.Users.AddRange(user, owner);

        var league = new League
        {
            Name = "Invalid Round League",
            OwnerId = owner.Id,
            Code = "INV01",
            Format = DeckFormat.Standard,
            TotalRounds = 3,
            CurrentRound = int.MaxValue, // Non-existent round ID
            RoundsToConsider = 3,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 5,
            TotalPlayers = 1,
            ScoringSystem = ScoringSystem.Positional,
            IsActive = true,
            IsPublic = true
        };

        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        context.UserLeagues.Add(new AppUserLeague
        {
            UserId = user.Id,
            User = user,
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

        var service = CreateService(context, user, owner);

        // Act
        var act = () => service.GetLeaguesForUserAsync(user.Id);

        // Assert
        var exception = await act.Should().ThrowAsync<LeagueServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        exception.Which.Message.Should().Contain("Errors.Leagues.InvalidCurrentRound");
    }

    [Fact]
    public async Task GetLeaguesForUserAsync_WithOwnedLeague_PopulatesCurrentRoundOrder()
    {
        // Arrange
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        context.Users.Add(owner);

        var league = new League
        {
            Name = "Owned League",
            OwnerId = owner.Id,
            Code = "OWN01",
            Format = DeckFormat.Standard,
            TotalRounds = 2,
            CurrentRound = 0,
            RoundsToConsider = 2,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 5,
            TotalPlayers = 0,
            ScoringSystem = ScoringSystem.Positional,
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

        league.CurrentRound = rounds[0].Id; // Set to first round (Order = 1)
        context.Leagues.Update(league);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner);

        // Act
        var result = await service.GetLeaguesForUserAsync(owner.Id);

        // Assert
        result.Should().NotBeNull();
        result.Leagues.Should().HaveCount(1);
        
        var returnedLeague = result.Leagues[0];
        returnedLeague.Id.Should().Be(league.Id);
        returnedLeague.CurrentRound.Should().Be(rounds[0].Id);
        returnedLeague.CurrentRoundOrder.Should().Be(1, "because the current round has Order = 1");
        returnedLeague.Score.Should().Be(0, "because owned leagues have Score = 0");
        returnedLeague.IsPlaying.Should().BeFalse("because owned leagues have IsPlaying = false");
    }

    [Fact]
    public async Task GetLeagueByIdAsync_PopulatesAdminIds_WithOwnerAndRoleAdmins()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var admin = CreateUser("admin");
        var member = CreateUser("member");
        context.Users.AddRange(owner, admin, member);

        var league = new League
        {
            Name = "League",
            OwnerId = owner.Id,
            Code = "ABC",
            Format = DeckFormat.Standard,
            TotalRounds = 1,
            CurrentRound = 0,
            RoundsToConsider = 1,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 0,
            TotalPlayers = 2,
            ScoringSystem = ScoringSystem.Positional,
            IsActive = true,
            IsPublic = true
        };
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        context.UserLeagues.AddRange(
            new AppUserLeague { UserId = owner.Id, User = owner, League = league, LeagueId = league.Id, Score = 0, IsPlaying = true },
            new AppUserLeague { UserId = admin.Id, User = admin, League = league, LeagueId = league.Id, Score = 0, IsPlaying = true },
            new AppUserLeague { UserId = member.Id, User = member, League = league, LeagueId = league.Id, Score = 0, IsPlaying = true });
        context.LeagueRoleAssignments.Add(new LeagueRoleAssignment
        {
            LeagueId = league.Id,
            League = league,
            UserId = admin.Id,
            User = admin,
            Roles = LeagueRole.Admin
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, admin, member);

        var result = await service.GetLeagueByIdAsync(league.Id, member.Id);

        result.Should().NotBeNull();
        result.AdminIds.Should().Contain(owner.Id);
        result.AdminIds.Should().Contain(admin.Id);
        result.AdminIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetLeaguesForUserAsync_PopulatesAdminIds_WithOwnerAndRoleAdmins()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var admin = CreateUser("admin");
        context.Users.AddRange(owner, admin);

        var league = new League
        {
            Name = "League",
            OwnerId = owner.Id,
            Code = "ABC",
            Format = DeckFormat.Standard,
            TotalRounds = 1,
            CurrentRound = 0,
            RoundsToConsider = 1,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 0,
            TotalPlayers = 1,
            ScoringSystem = ScoringSystem.Positional,
            IsActive = true,
            IsPublic = true
        };
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        context.UserLeagues.Add(new AppUserLeague
        {
            UserId = admin.Id,
            User = admin,
            League = league,
            LeagueId = league.Id,
            Score = 0,
            IsPlaying = true
        });
        context.LeagueRoleAssignments.Add(new LeagueRoleAssignment
        {
            LeagueId = league.Id,
            League = league,
            UserId = admin.Id,
            User = admin,
            Roles = LeagueRole.Admin
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, admin);

        var result = await service.GetLeaguesForUserAsync(admin.Id);

        result.Should().NotBeNull();
        result.Leagues.Should().HaveCount(1);
        result.Leagues[0].AdminIds.Should().Contain(owner.Id);
        result.Leagues[0].AdminIds.Should().Contain(admin.Id);
        result.Leagues[0].AdminIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRoundsByLeagueIdAsync_ReturnsAllRoundsOrderedByOrder()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        context.Users.Add(owner);

        var league = new League
        {
            Name = "Test League",
            OwnerId = owner.Id,
            Code = "TEST01",
            Format = DeckFormat.Standard,
            TotalRounds = 3,
            CurrentRound = 0,
            RoundsToConsider = 3,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 5,
            TotalPlayers = 0,
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
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner);

        var result = await service.GetRoundsByLeagueIdAsync(league.Id, owner.Id);

        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result[0].Order.Should().Be(1);
        result[1].Order.Should().Be(2);
        result[2].Order.Should().Be(3);
        result[0].Id.Should().Be(rounds[0].Id);
        result[1].Id.Should().Be(rounds[1].Id);
        result[2].Id.Should().Be(rounds[2].Id);
    }

    [Fact]
    public async Task GetRoundsByLeagueIdAsync_CreatesVirtualRoundsForMissingOrders()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        context.Users.Add(owner);

        var league = new League
        {
            Name = "Test League",
            OwnerId = owner.Id,
            Code = "TEST01",
            Format = DeckFormat.Standard,
            TotalRounds = 5,
            CurrentRound = 0,
            RoundsToConsider = 5,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 5,
            TotalPlayers = 0,
            ScoringSystem = ScoringSystem.Positional,
            IsActive = true,
            IsPublic = true
        };
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        // Create only rounds 1, 3, and 5
        var rounds = new List<Round>
        {
            new() { League = league, LeagueId = league.Id, Order = 1 },
            new() { League = league, LeagueId = league.Id, Order = 3 },
            new() { League = league, LeagueId = league.Id, Order = 5 }
        };
        context.Rounds.AddRange(rounds);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner);

        var result = await service.GetRoundsByLeagueIdAsync(league.Id, owner.Id);

        result.Should().NotBeNull();
        result.Should().HaveCount(5);
        
        // Round 1 exists
        result[0].Order.Should().Be(1);
        result[0].Id.Should().Be(rounds[0].Id);
        result[0].Status.Should().Be(Status.NotPlayed);
        
        // Round 2 is virtual
        result[1].Order.Should().Be(2);
        result[1].Id.Should().Be(0, "because virtual rounds have Id = 0");
        result[1].Status.Should().Be(Status.NotPlayed);
        result[1].Players.Should().BeEmpty();
        
        // Round 3 exists
        result[2].Order.Should().Be(3);
        result[2].Id.Should().Be(rounds[1].Id);
        
        // Round 4 is virtual
        result[3].Order.Should().Be(4);
        result[3].Id.Should().Be(0);
        result[3].Status.Should().Be(Status.NotPlayed);
        result[3].Players.Should().BeEmpty();
        
        // Round 5 exists
        result[4].Order.Should().Be(5);
        result[4].Id.Should().Be(rounds[2].Id);
    }

    [Fact]
    public async Task GetRoundsByLeagueIdAsync_ReturnsEmptyListWhenTotalRoundsIsZero()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        context.Users.Add(owner);

        var league = new League
        {
            Name = "Test League",
            OwnerId = owner.Id,
            Code = "TEST01",
            Format = DeckFormat.Standard,
            TotalRounds = 0,
            CurrentRound = 0,
            RoundsToConsider = 0,
            MinimumRounds = 0,
            TotalPrize = 0,
            PrizePerPerson = 5,
            TotalPlayers = 0,
            ScoringSystem = ScoringSystem.Positional,
            IsActive = true,
            IsPublic = true
        };
        context.Leagues.Add(league);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner);

        var result = await service.GetRoundsByLeagueIdAsync(league.Id, owner.Id);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRoundsByLeagueIdAsync_IncludesPlayersOrderedByPosition()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var player1 = CreateUser("player1");
        var player2 = CreateUser("player2");
        context.Users.AddRange(owner, player1, player2);

        var league = new League
        {
            Name = "Test League",
            OwnerId = owner.Id,
            Code = "TEST01",
            Format = DeckFormat.Standard,
            TotalRounds = 1,
            CurrentRound = 0,
            RoundsToConsider = 1,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 5,
            TotalPlayers = 2,
            ScoringSystem = ScoringSystem.Positional,
            IsActive = true,
            IsPublic = true
        };
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        var round = new Round
        {
            League = league,
            LeagueId = league.Id,
            Order = 1,
            Status = Status.Played
        };
        context.Rounds.Add(round);
        await context.SaveChangesAsync();

        context.UserRounds.AddRange(
            new AppUserRound
            {
                UserId = player2.Id,
                User = player2,
                RoundId = round.Id,
                Round = round,
                Position = 1,
                Score = 3,
                Wins = 2,
                Draws = 0,
                Losses = 0,
                Omw = 10,
                Gw = 20,
                Ogw = 30
            },
            new AppUserRound
            {
                UserId = player1.Id,
                User = player1,
                RoundId = round.Id,
                Round = round,
                Position = 2,
                Score = 1,
                Wins = 1,
                Draws = 0,
                Losses = 1,
                Omw = 5,
                Gw = 10,
                Ogw = 15
            });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, player1, player2);

        var result = await service.GetRoundsByLeagueIdAsync(league.Id, owner.Id);

        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].Players.Should().HaveCount(2);
        result[0].Players[0].UserId.Should().Be(player2.Id);
        result[0].Players[0].Position.Should().Be(1);
        result[0].Players[0].Score.Should().Be(3);
        result[0].Players[0].Wins.Should().Be(2);
        result[0].Players[1].UserId.Should().Be(player1.Id);
        result[0].Players[1].Position.Should().Be(2);
        result[0].Players[1].Score.Should().Be(1);
        result[0].Players[1].Wins.Should().Be(1);
    }

    [Fact]
    public async Task GetRoundsByLeagueIdAsync_WithNonExistentLeague_ThrowsNotFound()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        context.Users.Add(owner);
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner);

        var act = () => service.GetRoundsByLeagueIdAsync(999, owner.Id);

        var exception = await act.Should().ThrowAsync<LeagueServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        exception.Which.Message.Should().Contain("Errors.Leagues.NotFound");
    }

    [Fact]
    public async Task GetRoundsByLeagueIdAsync_WithUnauthorizedUser_ThrowsUnauthorized()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var unauthorized = CreateUser("unauthorized");
        context.Users.AddRange(owner, unauthorized);

        var league = new League
        {
            Name = "Test League",
            OwnerId = owner.Id,
            Code = "TEST01",
            Format = DeckFormat.Standard,
            TotalRounds = 1,
            CurrentRound = 0,
            RoundsToConsider = 1,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 5,
            TotalPlayers = 0,
            ScoringSystem = ScoringSystem.Positional,
            IsActive = true,
            IsPublic = false
        };
        context.Leagues.Add(league);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, unauthorized);

        var act = () => service.GetRoundsByLeagueIdAsync(league.Id, unauthorized.Id);

        var exception = await act.Should().ThrowAsync<LeagueServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        exception.Which.Message.Should().Contain("Errors.Leagues.Unauthorized");
    }

    [Fact]
    public async Task GetRoundsByLeagueIdAsync_WithMemberUser_ReturnsRounds()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var member = CreateUser("member");
        context.Users.AddRange(owner, member);

        var league = new League
        {
            Name = "Test League",
            OwnerId = owner.Id,
            Code = "TEST01",
            Format = DeckFormat.Standard,
            TotalRounds = 2,
            CurrentRound = 0,
            RoundsToConsider = 2,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 5,
            TotalPlayers = 1,
            ScoringSystem = ScoringSystem.Positional,
            IsActive = true,
            IsPublic = true
        };
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        context.UserLeagues.Add(new AppUserLeague
        {
            UserId = member.Id,
            User = member,
            League = league,
            LeagueId = league.Id,
            Score = 0,
            IsPlaying = true
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, member);

        var result = await service.GetRoundsByLeagueIdAsync(league.Id, member.Id);

        result.Should().NotBeNull();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRoundsByLeagueIdAsync_WithAllVirtualRounds_ReturnsAllVirtualRounds()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        context.Users.Add(owner);

        var league = new League
        {
            Name = "Test League",
            OwnerId = owner.Id,
            Code = "TEST01",
            Format = DeckFormat.Standard,
            TotalRounds = 3,
            CurrentRound = 0,
            RoundsToConsider = 3,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 5,
            TotalPlayers = 0,
            ScoringSystem = ScoringSystem.Positional,
            IsActive = true,
            IsPublic = true
        };
        context.Leagues.Add(league);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner);

        var result = await service.GetRoundsByLeagueIdAsync(league.Id, owner.Id);

        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result[0].Order.Should().Be(1);
        result[0].Id.Should().Be(0);
        result[0].Status.Should().Be(Status.NotPlayed);
        result[0].Players.Should().BeEmpty();
        result[1].Order.Should().Be(2);
        result[1].Id.Should().Be(0);
        result[2].Order.Should().Be(3);
        result[2].Id.Should().Be(0);
    }

    [Fact]
    public async Task ExportLeagueToExcelAsync_GeneratesValidExcelFile()
    {
        // Arrange
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var player1 = CreateUser("player1");
        player1.FirstName = "Alice";
        player1.LastName = "Johnson";
        var player2 = CreateUser("player2");
        player2.FirstName = "Bob";
        player2.LastName = "Smith";
        var player3 = CreateUser("player3");
        player3.FirstName = "Charlie";
        player3.LastName = "Brown";

        context.Users.AddRange(owner, player1, player2, player3);

        var league = new League
        {
            Name = "Test League Export",
            OwnerId = owner.Id,
            Code = "EXP01",
            Format = DeckFormat.Modern,
            TotalRounds = 3,
            CurrentRound = 0,
            RoundsToConsider = 3,
            MinimumRounds = 1,
            TotalPrize = 0,
            PrizePerPerson = 0,
            TotalPlayers = 3,
            ScoringSystem = ScoringSystem.Positional,
            IsActive = true,
            IsPublic = true
        };

        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        context.LeagueRoleAssignments.Add(new LeagueRoleAssignment
        {
            LeagueId = league.Id,
            UserId = owner.Id,
            Roles = LeagueRole.Admin
        });

        context.UserLeagues.AddRange(
            new AppUserLeague { UserId = player1.Id, User = player1, LeagueId = league.Id, Score = 8, IsPlaying = true, Rounds = [3, 5, 0] },
            new AppUserLeague { UserId = player2.Id, User = player2, LeagueId = league.Id, Score = 5, IsPlaying = true, Rounds = [2, 3, 0] },
            new AppUserLeague { UserId = player3.Id, User = player3, LeagueId = league.Id, Score = 3, IsPlaying = true, Rounds = [1, 0, 0] }
        );
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, player1, player2, player3);

        // Act
        var excelBytes = await service.ExportLeagueToExcelAsync(league.Id, owner.Id);

        // Assert
        excelBytes.Should().NotBeNull();
        excelBytes.Length.Should().BeGreaterThan(0);

        using var stream = new MemoryStream(excelBytes);
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);

        var worksheet = workbook.Worksheets.First();
        worksheet.Name.Should().Be("Test League Export");

        worksheet.Cell(1, 1).Value.ToString().Should().Be("Player Name");
        worksheet.Cell(1, 2).Value.ToString().Should().Be("Round 1");
        worksheet.Cell(1, 3).Value.ToString().Should().Be("Round 2");
        worksheet.Cell(1, 4).Value.ToString().Should().Be("Round 3");
        worksheet.Cell(1, 5).Value.ToString().Should().Be("Total Points");

        worksheet.Cell(1, 1).Style.Font.Bold.Should().BeTrue();
        worksheet.Cell(1, 2).Style.Font.Bold.Should().BeTrue();
        worksheet.Cell(1, 5).Style.Font.Bold.Should().BeTrue();

        // Alice: Rounds [3, 5, 0] -> Round 1=3, Round 2=5, Round 3=blank, Total=8
        worksheet.Cell(2, 1).Value.ToString().Should().Be("Alice Johnson");
        worksheet.Cell(2, 2).Value.ToString().Should().Be("3");
        worksheet.Cell(2, 3).Value.ToString().Should().Be("5");
        worksheet.Cell(2, 4).Value.ToString().Should().BeEmpty();
        worksheet.Cell(2, 5).Value.ToString().Should().Be("8");

        // Bob: Rounds [2, 3, 0] -> Round 1=2, Round 2=3, Round 3=blank, Total=5
        worksheet.Cell(3, 1).Value.ToString().Should().Be("Bob Smith");
        worksheet.Cell(3, 2).Value.ToString().Should().Be("2");
        worksheet.Cell(3, 3).Value.ToString().Should().Be("3");
        worksheet.Cell(3, 4).Value.ToString().Should().BeEmpty();
        worksheet.Cell(3, 5).Value.ToString().Should().Be("5");

        // Charlie: Rounds [1, 0, 0] -> Round 1=1, Round 2=blank, Round 3=blank, Total=3
        worksheet.Cell(4, 1).Value.ToString().Should().Be("Charlie Brown");
        worksheet.Cell(4, 2).Value.ToString().Should().Be("1");
        worksheet.Cell(4, 3).Value.ToString().Should().BeEmpty();
        worksheet.Cell(4, 4).Value.ToString().Should().BeEmpty();
        worksheet.Cell(4, 5).Value.ToString().Should().Be("3");
    }

    [Fact]
    public async Task ExportLeagueToExcelAsync_RequiresAdminPermissions()
    {
        // Arrange
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var nonAdmin = CreateUser("nonadmin");
        context.Users.AddRange(owner, nonAdmin);

        var league = CreateLeague(owner.Id, "PERM01");
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        context.LeagueRoleAssignments.Add(new LeagueRoleAssignment
        {
            LeagueId = league.Id,
            UserId = owner.Id,
            Roles = LeagueRole.Admin
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, nonAdmin);

        // Act
        var act = () => service.ExportLeagueToExcelAsync(league.Id, nonAdmin.Id);

        // Assert
        var exception = await act.Should().ThrowAsync<LeagueServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        exception.Which.Message.Should().Be("Errors.Leagues.AdminOnly");
    }

    [Fact]
    public async Task ExportLeagueToExcelAsync_WithNonExistentLeague_ThrowsNotFoundException()
    {
        // Arrange
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        context.Users.Add(owner);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner);

        // Act
        var act = () => service.ExportLeagueToExcelAsync(999, owner.Id);

        // Assert
        var exception = await act.Should().ThrowAsync<LeagueServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        exception.Which.Message.Should().Be("Errors.Leagues.NotFound");
    }

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

    private static LeagueService CreateService(MainContext context, params AppUser[] users)
    {
        var uow = CreateUow(context);
        var manager = CreateUserManagerMock(users);
        var validationService = new ValidationService();
        var notificationService = new NotificationService(uow, NullLogger<NotificationService>.Instance);
        return new LeagueService(manager.Object, uow, validationService, notificationService, NullLogger<LeagueService>.Instance);
    }

    private static Mock<UserManager<AppUser>> CreateUserManagerMock(params AppUser[] users)
    {
        var store = new Mock<IUserStore<AppUser>>();
        var manager = new Mock<UserManager<AppUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        foreach (var user in users)
        {
            manager.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        }
        return manager;
    }

    [Fact]
    public async Task ParseEventLinkResultsAsync_WithMatchingCompanionNames_MatchesUsers()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var player = CreateUser("player");
        player.CompanionName = "Fabio Paglieri";
        context.Users.AddRange(owner, player);

        var league = CreateLeague(owner.Id, "PDF01");
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        context.UserLeagues.Add(new AppUserLeague
        {
            UserId = player.Id, User = player, League = league, LeagueId = league.Id,
            Score = 0, IsPlaying = true
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, player);
        var file = CreateMockPdfFile("TestData/finale.pdf");

        var result = await service.ParseEventLinkResultsAsync(league.Id, owner.Id, file);

        result.Players.Should().HaveCount(38);
        var matched = result.Players.First(p => p.PdfName == "Fabio Paglieri");
        matched.Matched.Should().BeTrue();
        matched.UserId.Should().Be(player.Id);
        matched.Score.Should().Be(21);
        matched.Omw.Should().Be(54);
        matched.Gw.Should().Be(73);
        matched.Ogw.Should().Be(57);
    }

    [Fact]
    public async Task ParseEventLinkResultsAsync_WithNoMatchingCompanionName_ReturnsUnmatched()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var player = CreateUser("player");
        player.CompanionName = "NonExistent Name";
        context.Users.AddRange(owner, player);

        var league = CreateLeague(owner.Id, "UNM01");
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        context.UserLeagues.Add(new AppUserLeague
        {
            UserId = player.Id, User = player, League = league, LeagueId = league.Id,
            Score = 0, IsPlaying = true
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, player);
        var file = CreateMockPdfFile("TestData/finale.pdf");

        var result = await service.ParseEventLinkResultsAsync(league.Id, owner.Id, file);

        result.Players.Should().OnlyContain(p => !p.Matched);
        result.Players.Should().OnlyContain(p => p.UserId.StartsWith("UNMATCHED:"));
    }

    [Fact]
    public async Task ParseEventLinkResultsAsync_WithDuplicateCompanionNames_FlagsDuplicates()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var player1 = CreateUser("player-1");
        var player2 = CreateUser("player-2");
        player1.CompanionName = "Fabio Paglieri";
        player2.CompanionName = "Fabio Paglieri";
        context.Users.AddRange(owner, player1, player2);

        var league = CreateLeague(owner.Id, "DUP01");
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        context.UserLeagues.AddRange(
            new AppUserLeague { UserId = player1.Id, User = player1, League = league, LeagueId = league.Id, Score = 0, IsPlaying = true },
            new AppUserLeague { UserId = player2.Id, User = player2, League = league, LeagueId = league.Id, Score = 0, IsPlaying = true });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, player1, player2);
        var file = CreateMockPdfFile("TestData/finale.pdf");

        var result = await service.ParseEventLinkResultsAsync(league.Id, owner.Id, file);

        var fabio = result.Players.First(p => p.PdfName == "Fabio Paglieri");
        fabio.Matched.Should().BeFalse();
        fabio.UserId.Should().StartWith("DUPLICATE:");
        result.Errors.Should().Contain(e => e.Contains("Duplicate companion name"));
    }

    [Fact]
    public async Task ParseEventLinkResultsAsync_WithNonPlayingMember_DoesNotMatch()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var inactivePlayer = CreateUser("inactive");
        inactivePlayer.CompanionName = "Fabio Paglieri";
        context.Users.AddRange(owner, inactivePlayer);

        var league = CreateLeague(owner.Id, "INA01");
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        context.UserLeagues.Add(new AppUserLeague
        {
            UserId = inactivePlayer.Id, User = inactivePlayer, League = league, LeagueId = league.Id,
            Score = 0, IsPlaying = false
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, inactivePlayer);
        var file = CreateMockPdfFile("TestData/finale.pdf");

        var result = await service.ParseEventLinkResultsAsync(league.Id, owner.Id, file);

        var fabio = result.Players.First(p => p.PdfName == "Fabio Paglieri");
        fabio.Matched.Should().BeFalse();
        fabio.UserId.Should().StartWith("UNMATCHED:");
    }

    [Fact]
    public async Task ParseEventLinkResultsAsync_WithNonAdmin_ThrowsUnauthorized()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var nonAdmin = CreateUser("nonadmin");
        context.Users.AddRange(owner, nonAdmin);

        var league = CreateLeague(owner.Id, "AUTH01");
        context.Leagues.Add(league);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, nonAdmin);
        var file = CreateMockPdfFile("TestData/finale.pdf");

        var act = () => service.ParseEventLinkResultsAsync(league.Id, nonAdmin.Id, file);

        var exception = await act.Should().ThrowAsync<LeagueServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task ParseEventLinkResultsAsync_WithNonExistentLeague_ThrowsNotFound()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        context.Users.Add(owner);
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner);
        var file = CreateMockPdfFile("TestData/finale.pdf");

        var act = () => service.ParseEventLinkResultsAsync(999, owner.Id, file);

        var exception = await act.Should().ThrowAsync<LeagueServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ParseEventLinkResultsAsync_MatchesCaseInsensitively()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var player = CreateUser("player");
        player.CompanionName = "fabio paglieri";
        context.Users.AddRange(owner, player);

        var league = CreateLeague(owner.Id, "CASE01");
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        context.UserLeagues.Add(new AppUserLeague
        {
            UserId = player.Id, User = player, League = league, LeagueId = league.Id,
            Score = 0, IsPlaying = true
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, player);
        var file = CreateMockPdfFile("TestData/finale.pdf");

        var result = await service.ParseEventLinkResultsAsync(league.Id, owner.Id, file);

        var matched = result.Players.First(p => p.PdfName == "Fabio Paglieri");
        matched.Matched.Should().BeTrue();
        matched.UserId.Should().Be(player.Id);
    }

    [Fact]
    public async Task JoinRoundAsync_WithValidPlayer_AddsParticipant()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var player = CreateUser("player");
        context.Users.AddRange(owner, player);

        var league = CreateLeague(owner.Id, "JOIN01");
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        var round = new Round { League = league, LeagueId = league.Id, Order = 1, Status = Status.Playing };
        context.Rounds.Add(round);
        await context.SaveChangesAsync();

        context.UserLeagues.Add(new AppUserLeague
        {
            UserId = player.Id, User = player, League = league, LeagueId = league.Id,
            Score = 0, IsPlaying = true
        });
        await context.SaveChangesAsync();

        var service = CreateService(context, owner, player);

        await service.JoinRoundAsync(league.Id, round.Id, player.Id);

        var participants = await context.RoundParticipants.ToListAsync();
        participants.Should().HaveCount(1);
        participants[0].UserId.Should().Be(player.Id);
        participants[0].RoundId.Should().Be(round.Id);
    }

    [Fact]
    public async Task JoinRoundAsync_WithNonMember_ThrowsBadRequest()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var outsider = CreateUser("outsider");
        context.Users.AddRange(owner, outsider);

        var league = CreateLeague(owner.Id, "JOIN02");
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        var round = new Round { League = league, LeagueId = league.Id, Order = 1, Status = Status.Playing };
        context.Rounds.Add(round);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, outsider);

        var act = () => service.JoinRoundAsync(league.Id, round.Id, outsider.Id);

        var exception = await act.Should().ThrowAsync<LeagueServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        exception.Which.Message.Should().Contain("Errors.Leagues.UserMustBeMember");
    }

    [Fact]
    public async Task JoinRoundAsync_WithNonPlayingRound_ThrowsBadRequest()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var player = CreateUser("player");
        context.Users.AddRange(owner, player);

        var league = CreateLeague(owner.Id, "JOIN03");
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        var round = new Round { League = league, LeagueId = league.Id, Order = 1, Status = Status.NotPlayed };
        context.Rounds.Add(round);
        await context.SaveChangesAsync();

        context.UserLeagues.Add(new AppUserLeague
        {
            UserId = player.Id, User = player, League = league, LeagueId = league.Id,
            Score = 0, IsPlaying = true
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, player);

        var act = () => service.JoinRoundAsync(league.Id, round.Id, player.Id);

        var exception = await act.Should().ThrowAsync<LeagueServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        exception.Which.Message.Should().Contain("Errors.Leagues.RoundNotPlaying");
    }

    [Fact]
    public async Task JoinRoundAsync_AlreadyJoined_ThrowsBadRequest()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var player = CreateUser("player");
        context.Users.AddRange(owner, player);

        var league = CreateLeague(owner.Id, "JOIN04");
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        var round = new Round { League = league, LeagueId = league.Id, Order = 1, Status = Status.Playing };
        context.Rounds.Add(round);
        await context.SaveChangesAsync();

        context.UserLeagues.Add(new AppUserLeague
        {
            UserId = player.Id, User = player, League = league, LeagueId = league.Id,
            Score = 0, IsPlaying = true
        });
        context.RoundParticipants.Add(new RoundParticipant
        {
            UserId = player.Id, User = player, RoundId = round.Id, Round = round
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, player);

        var act = () => service.JoinRoundAsync(league.Id, round.Id, player.Id);

        var exception = await act.Should().ThrowAsync<LeagueServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        exception.Which.Message.Should().Contain("Errors.Leagues.AlreadyJoinedRound");
    }

    [Fact]
    public async Task LeaveRoundAsync_WithParticipant_RemovesParticipant()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var player = CreateUser("player");
        context.Users.AddRange(owner, player);

        var league = CreateLeague(owner.Id, "LEAVE01");
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        var round = new Round { League = league, LeagueId = league.Id, Order = 1, Status = Status.Playing };
        context.Rounds.Add(round);
        await context.SaveChangesAsync();

        context.RoundParticipants.Add(new RoundParticipant
        {
            UserId = player.Id, User = player, RoundId = round.Id, Round = round
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, player);

        await service.LeaveRoundAsync(league.Id, round.Id, player.Id);

        var participants = await context.RoundParticipants.ToListAsync();
        participants.Should().BeEmpty();
    }

    [Fact]
    public async Task LeaveRoundAsync_WithNonParticipant_ThrowsNotFound()
    {
        await using var context = CreateContext();
        var owner = CreateUser("owner");
        var player = CreateUser("player");
        context.Users.AddRange(owner, player);

        var league = CreateLeague(owner.Id, "LEAVE02");
        context.Leagues.Add(league);
        await context.SaveChangesAsync();

        var round = new Round { League = league, LeagueId = league.Id, Order = 1, Status = Status.Playing };
        context.Rounds.Add(round);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var service = CreateService(context, owner, player);

        var act = () => service.LeaveRoundAsync(league.Id, round.Id, player.Id);

        var exception = await act.Should().ThrowAsync<LeagueServiceException>();
        exception.Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        exception.Which.Message.Should().Contain("Errors.Leagues.NotFound");
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

    private static League CreateLeague(string ownerId, string code) => new()
    {
        Name = "Test League",
        OwnerId = ownerId,
        Code = code,
        Format = DeckFormat.Modern,
        TotalRounds = 1,
        CurrentRound = 0,
        RoundsToConsider = 1,
        MinimumRounds = 1,
        TotalPrize = 0,
        PrizePerPerson = 0,
        TotalPlayers = 0,
        ScoringSystem = ScoringSystem.Positional,
        IsActive = true,
        IsPublic = true
    };

    private static IFormFile CreateMockPdfFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var stream = new MemoryStream(bytes);
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.OpenReadStream()).Returns(stream);
        mock.Setup(f => f.FileName).Returns("test.pdf");
        mock.Setup(f => f.Length).Returns(bytes.Length);
        return mock.Object;
    }
}
