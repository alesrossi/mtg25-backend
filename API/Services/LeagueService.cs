using System.Security.Cryptography;
using System.Text;
using API.Dtos.Leagues;
using API.Dtos.Notifications;
using Core.Enums;
using Core.Models.Identity;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Services;

public interface ILeagueService
{
    Task<IReadOnlyList<League>> GetPublicLeaguesAsync(string userId, CancellationToken cancellationToken = default);
    Task<UserWithLeaguesDto> GetLeaguesForUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<LeagueDto> GetLeagueByIdAsync(int leagueId, string userId, CancellationToken cancellationToken = default);
    Task<LeagueWithScoresDto> GetLeagueScoresAsync(int leagueId, string userId, CancellationToken cancellationToken = default);
    Task<RoundInfoDto> GetRoundByIdAsync(int leagueId, int roundId, string userId, CancellationToken cancellationToken = default);
    Task<League> UpdateLeagueAsync(int leagueId, string userId, UpdateLeagueDto updateLeague, CancellationToken cancellationToken = default);
    Task<Round> UpdateRoundAsync(int leagueId, int roundId, string userId, UpdateRoundDto updateRound, CancellationToken cancellationToken = default);
    Task UpdateLeagueResultsAsync(int leagueId, string userId, List<UserWithScore> userList, CancellationToken cancellationToken = default);
    Task<string> GetInviteCodeAsync(int leagueId, string userId, CancellationToken cancellationToken = default);
    Task RequestJoinLeagueAsync(string code, string userId, CancellationToken cancellationToken = default);
    Task JoinLeagueAsync(int leagueId, string userId, CancellationToken cancellationToken = default);
    Task LeaveLeagueAsync(int leagueId, string userId, CancellationToken cancellationToken = default);
    Task<League> CreateLeagueAsync(string userId, NewLeagueDto leagueDto, CancellationToken cancellationToken = default);
    Task JoinAsPlayerAsync(int leagueId, string userId, CancellationToken cancellationToken = default);
    Task PromoteLeagueAdminAsync(int leagueId, string userId, string targetUserId, CancellationToken cancellationToken = default);
    Task TerminateLeagueAsync(int leagueId, string userId, CancellationToken cancellationToken = default);
}

public sealed class LeagueService : ILeagueService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppIdentityDbContext _dbContext;
    private readonly IValidationService _validationService;
    private readonly NotificationService _notificationService;
    private readonly ILogger<LeagueService> _logger;

    public LeagueService(
        UserManager<AppUser> userManager,
        AppIdentityDbContext dbContext,
        IValidationService validationService,
        NotificationService notificationService,
        ILogger<LeagueService> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _validationService = validationService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<League>> GetPublicLeaguesAsync(string userId, CancellationToken cancellationToken = default)
    {
        await EnsureUserAsync(userId);

        return await _dbContext.Leagues
            .AsNoTracking()
            .Where(l => l.IsPublic)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserWithLeaguesDto> GetLeaguesForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await EnsureUserAsync(userId);

        var res = await _dbContext.UserLeagues
            .Where(ul => ul.UserId == userId)
            .Include(ul => ul.League)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var leaguesById = res
            .Select(x => new LeagueDto
            {
                Code = x.League!.Code,
                Name = x.League.Name,
                Score = x.Score,
                Id = x.LeagueId,
                Format = x.League.Format,
                TotalRounds = x.League.TotalRounds,
                CurrentRound = x.League.CurrentRound,
                RoundsToConsider = x.League.RoundsToConsider,
                MinimumRounds = x.League.MinimumRounds,
                TotalPrize = x.League.TotalPrize,
                PrizePerPerson = x.League.PrizePerPerson,
                TotalPlayers = x.League.TotalPlayers,
                IsActive = x.League.IsActive,
                IsPlaying = x.IsPlaying,
                ScoringSystem = x.League.ScoringSystem,
                OwnerId = x.League.OwnerId,
                IsPublic = x.League.IsPublic
            })
            .ToDictionary(league => league.Id);

        var ownedLeagues = await _dbContext.Leagues
            .Where(l => l.OwnerId == userId)
            .Select(league => new LeagueDto
            {
                Code = league.Code,
                Name = league.Name,
                Score = 0,
                Id = league.Id,
                Format = league.Format,
                TotalRounds = league.TotalRounds,
                CurrentRound = league.CurrentRound,
                RoundsToConsider = league.RoundsToConsider,
                MinimumRounds = league.MinimumRounds,
                TotalPrize = league.TotalPrize,
                PrizePerPerson = league.PrizePerPerson,
                TotalPlayers = league.TotalPlayers,
                IsActive = league.IsActive,
                IsPlaying = false,
                ScoringSystem = league.ScoringSystem,
                OwnerId = league.OwnerId,
                IsPublic = league.IsPublic
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        foreach (var league in ownedLeagues)
        {
            leaguesById.TryAdd(league.Id, league);
        }

        return new UserWithLeaguesDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = user.DisplayName,
            Leagues = leaguesById.Values.ToList()
        };
    }

    public async Task<LeagueDto> GetLeagueByIdAsync(int leagueId, string userId, CancellationToken cancellationToken = default)
    {
        await EnsureUserAsync(userId);

        var league = await _dbContext.Leagues
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == leagueId, cancellationToken);
        if (league is null)
        {
            throw LeagueServiceException.NotFound("Errors.Leagues.NotFound");
        }

        var res = await _dbContext.UserLeagues
            .Where(ul => ul.LeagueId == league.Id && ul.UserId == userId)
            .Include(ul => ul.League)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (userId != league.OwnerId && res is null)
        {
            throw LeagueServiceException.Unauthorized("Errors.Leagues.UserNotInLeague");
        }

        return new LeagueDto
        {
            Id = league.Id,
            Name = league.Name,
            Code = league.Code,
            Format = league.Format,
            TotalRounds = league.TotalRounds,
            CurrentRound = league.CurrentRound,
            RoundsToConsider = league.RoundsToConsider,
            MinimumRounds = league.MinimumRounds,
            TotalPrize = league.TotalPrize,
            PrizePerPerson = league.PrizePerPerson,
            TotalPlayers = league.TotalPlayers,
            Score = res!.Score,
            ScoringSystem = league.ScoringSystem,
            OwnerId = league.OwnerId,
            IsActive = league.IsActive,
            IsPlaying = res.IsPlaying,
            IsPublic = league.IsPublic
        };
    }

    public async Task<LeagueWithScoresDto> GetLeagueScoresAsync(int leagueId, string userId, CancellationToken cancellationToken = default)
    {
        await EnsureUserAsync(userId);

        var league = await _dbContext.Leagues
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == leagueId, cancellationToken);
        if (league is null)
        {
            throw LeagueServiceException.NotFound("Errors.Leagues.NotFound");
        }

        var res = await _dbContext.UserLeagues
            .Where(ul => ul.LeagueId == league.Id)
            .Include(ul => ul.User)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var leagueWithScores = new LeagueWithScoresDto
        {
            Id = league.Id,
            Name = league.Name,
            OwnerId = league.OwnerId,
            CurrentRound = league.CurrentRound,
            ScoringSystem = league.ScoringSystem,
            IsPublic = league.IsPublic
        };

        var orderedPlayers = res
            .Where(x => x.LeagueId == league.Id && x.IsPlaying)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.User!.FirstName)
            .ThenBy(x => x.User!.LastName);

        foreach (var player in orderedPlayers)
        {
            leagueWithScores.Scores.Add(new Score
            {
                FirstName = player.User!.FirstName,
                LastName = player.User.LastName,
                UserId = player.User.Id,
                Points = player.Score,
                RoundsPlayed = player.RoundsPlayed,
                BestRound = player.BestRound,
                AvgPosition = player.AvgPosition,
                Rounds = player.Rounds
            });
        }

        return leagueWithScores;
    }

    public async Task<RoundInfoDto> GetRoundByIdAsync(int leagueId, int roundId, string userId, CancellationToken cancellationToken = default)
    {
        await EnsureUserAsync(userId);

        var league = await _dbContext.Leagues
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == leagueId, cancellationToken);
        if (league is null)
        {
            throw LeagueServiceException.NotFound("Errors.Leagues.NotFound");
        }

        var membership = await _dbContext.UserLeagues
            .AsNoTracking()
            .FirstOrDefaultAsync(ul => ul.LeagueId == leagueId && ul.UserId == userId, cancellationToken);
        if (userId != league.OwnerId && membership is null)
        {
            throw LeagueServiceException.Unauthorized("Errors.Leagues.UserNotInLeague");
        }

        var round = await _dbContext.Rounds
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roundId && r.LeagueId == leagueId, cancellationToken);
        if (round is null)
        {
            throw LeagueServiceException.NotFound("Errors.Leagues.NotFound");
        }

        var userRounds = await _dbContext.UserRounds
            .AsNoTracking()
            .Where(ur => ur.RoundId == roundId)
            .OrderBy(ur => ur.Position)
            .Select(ur => new UserRoundInfoDto
            {
                UserId = ur.UserId,
                Position = ur.Position,
                Score = ur.Score,
                Wins = ur.Wins,
                Draws = ur.Draws,
                Losses = ur.Losses,
                Omw = ur.Omw,
                Gw = ur.Gw,
                Ogw = ur.Ogw
            })
            .ToListAsync(cancellationToken);

        return new RoundInfoDto
        {
            Id = round.Id,
            Status = round.Status,
            StartDate = round.StartDate,
            Description = round.Description,
            Order = round.Order,
            LeagueId = round.LeagueId,
            Players = userRounds
        };
    }

    public async Task<League> UpdateLeagueAsync(int leagueId, string userId, UpdateLeagueDto updateLeague, CancellationToken cancellationToken = default)
    {
        var user = await EnsureUserAsync(userId);

        var league = await _dbContext.Leagues
            .AsTracking()
            .FirstOrDefaultAsync(l => l.Id == leagueId, cancellationToken);
        if (league is null)
        {
            throw LeagueServiceException.NotFound("Errors.Leagues.NotFound");
        }

        if (!await IsLeagueAdminAsync(league, user.Id, cancellationToken))
        {
            throw LeagueServiceException.Unauthorized("Errors.Leagues.Unauthorized");
        }

        var (isValid, errors) = _validationService.ValidateModel(updateLeague);
        if (!isValid)
        {
            throw LeagueServiceException.ValidationFailed(errors, "Errors.Leagues.ValidationFailed");
        }

        if (updateLeague.Name != null) league.Name = updateLeague.Name;
        if (updateLeague.TotalRounds != null) league.TotalRounds = (int)updateLeague.TotalRounds;
        if (updateLeague.RoundsToConsider != null) league.RoundsToConsider = (int)updateLeague.RoundsToConsider;
        if (updateLeague.MinimumRounds != null) league.MinimumRounds = (int)updateLeague.MinimumRounds;
        if (updateLeague.TotalPrize != null) league.TotalPrize = (double)updateLeague.TotalPrize;
        if (updateLeague.PrizePerPerson != null) league.PrizePerPerson = (double)updateLeague.PrizePerPerson;
        if (updateLeague.IsPublic.HasValue) league.IsPublic = updateLeague.IsPublic.Value;
        if (updateLeague.CurrentRound == null)
        {
            throw LeagueServiceException.BadRequest("Errors.Leagues.InvalidCurrentRound");
        }

        var currentRound = await _dbContext.Rounds
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == updateLeague.CurrentRound && r.LeagueId == league.Id, cancellationToken);
        if (currentRound is null)
        {
            throw LeagueServiceException.BadRequest("Errors.Leagues.InvalidCurrentRound");
        }

        league.CurrentRound = currentRound.Id;

        _dbContext.Update(league);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return league;
    }

    public async Task<Round> UpdateRoundAsync(
        int leagueId,
        int roundId,
        string userId,
        UpdateRoundDto updateRound,
        CancellationToken cancellationToken = default)
    {
        var user = await EnsureUserAsync(userId);

        var league = await _dbContext.Leagues
            .AsTracking()
            .FirstOrDefaultAsync(l => l.Id == leagueId, cancellationToken);
        if (league is null)
        {
            throw LeagueServiceException.NotFound("Errors.Leagues.NotFound");
        }

        if (!await IsLeagueAdminAsync(league, user.Id, cancellationToken))
        {
            throw LeagueServiceException.Unauthorized("Errors.Leagues.Unauthorized");
        }

        var round = await _dbContext.Rounds
            .AsTracking()
            .FirstOrDefaultAsync(r => r.Id == roundId && r.LeagueId == leagueId, cancellationToken);
        if (round is null)
        {
            throw LeagueServiceException.NotFound("Errors.Leagues.NotFound");
        }

        var (isValid, errors) = _validationService.ValidateModel(updateRound);
        if (!isValid)
        {
            throw LeagueServiceException.ValidationFailed(errors, "Errors.Leagues.ValidationFailed");
        }

        if (updateRound.StartDate.HasValue)
        {
            round.StartDate = updateRound.StartDate;
        }

        if (updateRound.Description != null)
        {
            round.Description = updateRound.Description;
        }

        var hasResultData = false;
        if (updateRound.Players != null)
        {
            var requestedPlayers = updateRound.Players;
            var requestedUserIds = requestedPlayers
                .Select(player => player.UserId)
                .ToList();
            if (requestedUserIds.Any(string.IsNullOrWhiteSpace))
            {
                throw LeagueServiceException.BadRequest("Errors.Leagues.ValidationFailed");
            }

            var distinctPlayerIds = requestedUserIds
                .Distinct()
                .ToList();
            if (distinctPlayerIds.Count != requestedUserIds.Count)
            {
                throw LeagueServiceException.BadRequest("Errors.Leagues.ValidationFailed");
            }

            var eligiblePlayerIds = await _dbContext.UserLeagues
                .Where(ul => ul.LeagueId == leagueId && ul.IsPlaying)
                .Select(ul => ul.UserId)
                .ToListAsync(cancellationToken);

            var invalidPlayerIds = distinctPlayerIds
                .Except(eligiblePlayerIds)
                .ToList();

            if (invalidPlayerIds.Count > 0)
            {
                throw LeagueServiceException.BadRequest("Errors.Leagues.UserMustBeMember", includeBody: true);
            }

            var existingRounds = await _dbContext.UserRounds
                .Where(ur => ur.RoundId == round.Id)
                .ToListAsync(cancellationToken);
            _dbContext.UserRounds.RemoveRange(existingRounds);

            if (distinctPlayerIds.Count > 0)
            {
                var users = await _dbContext.Users
                    .Where(u => distinctPlayerIds.Contains(u.Id))
                    .ToListAsync(cancellationToken);
                var userLookup = users.ToDictionary(user2 => user2.Id);
                hasResultData = requestedPlayers.Any(player =>
                    player.Position.HasValue ||
                    player.Wins.HasValue ||
                    player.Draws.HasValue ||
                    player.Losses.HasValue ||
                    player.Omw.HasValue ||
                    player.Gw.HasValue ||
                    player.Ogw.HasValue);

                if (!hasResultData)
                {
                    var userRounds = users
                        .Select(user2 => new AppUserRound
                        {
                            UserId = user2.Id,
                            User = user2,
                            RoundId = round.Id,
                            Round = round,
                            Position = 0,
                            Score = 0
                        })
                        .ToList();

                    await _dbContext.UserRounds.AddRangeAsync(userRounds, cancellationToken);
                    round.Players = userRounds;
                }
                else
                {
                    var entries = requestedPlayers
                        .Select((player, index) => new RoundResultEntry
                        {
                            Player = player,
                            Index = index,
                            Score = league.ScoringSystem == ScoringSystem.Victories
                                ? (player.Wins ?? 0) * (league.PointsPerWin ?? 0)
                                  + (player.Draws ?? 0) * (league.PointsPerDraw ?? 0)
                                  + (player.Losses ?? 0) * (league.PointsPerLoss ?? 0)
                                : 0
                        })
                        .ToList();

                    var useProvidedPositions = false;
                    if (league.ScoringSystem == ScoringSystem.Victories)
                    {
                        entries = entries
                            .OrderByDescending(entry => entry.Score)
                            .ThenByDescending(entry => entry.Player.Omw ?? 0)
                            .ThenByDescending(entry => entry.Player.Gw ?? 0)
                            .ThenByDescending(entry => entry.Player.Ogw ?? 0)
                            .ThenBy(entry => entry.Index)
                            .ToList();
                    }
                    else
                    {
                        var positionsProvided = requestedPlayers.All(player =>
                            player.Position.HasValue && player.Position.Value > 0);
                        var positionsUnique = positionsProvided
                            && requestedPlayers.Select(player => player.Position!.Value).Distinct().Count() == requestedPlayers.Count;
                        useProvidedPositions = positionsProvided && positionsUnique;
                        if (useProvidedPositions)
                        {
                            entries = entries
                                .OrderBy(entry => entry.Player.Position!.Value)
                                .ThenBy(entry => entry.Index)
                                .ToList();
                        }
                    }

                    var userRounds = new List<AppUserRound>();
                    for (var index = 0; index < entries.Count; index++)
                    {
                        var entry = entries[index];
                        if (!userLookup.TryGetValue(entry.Player.UserId, out var user2))
                        {
                            throw LeagueServiceException.NotFound("Errors.Leagues.UserNotFound");
                        }

                        var position = league.ScoringSystem == ScoringSystem.Positional && useProvidedPositions
                            ? entry.Player.Position!.Value
                            : index + 1;
                        var roundScore = league.ScoringSystem == ScoringSystem.Positional
                            ? league.PointsToGive != null && league.PointsToGive.Count >= position
                                ? league.PointsToGive[position - 1]
                                : 0
                            : entry.Score;

                        userRounds.Add(new AppUserRound
                        {
                            UserId = user2.Id,
                            User = user2,
                            RoundId = round.Id,
                            Round = round,
                            Position = position,
                            Score = roundScore,
                            Wins = entry.Player.Wins ?? 0,
                            Draws = entry.Player.Draws ?? 0,
                            Losses = entry.Player.Losses ?? 0,
                            Omw = entry.Player.Omw ?? 0,
                            Gw = entry.Player.Gw ?? 0,
                            Ogw = entry.Player.Ogw ?? 0
                        });
                    }

                    round.Players = userRounds;
                    round.Status = Status.Played;

                    await _dbContext.UserRounds.AddRangeAsync(userRounds, cancellationToken);
                    _dbContext.Update(league);
                }
            }
            else
            {
                round.Players = [];
            }
        }

        _dbContext.Update(round);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (hasResultData)
        {
            await RecalculateLeagueStandingsAsync(league, cancellationToken);
        }

        return round;
    }

    private async Task RecalculateLeagueStandingsAsync(League league, CancellationToken cancellationToken)
    {
        var userLeagues = await _dbContext.UserLeagues
            .Where(ul => ul.LeagueId == league.Id && ul.IsPlaying)
            .ToListAsync(cancellationToken);

        foreach (var userLeague in userLeagues)
        {
            userLeague.Score = 0;
            userLeague.RoundsPlayed = 0;
            userLeague.BestRound = 0;
            userLeague.AvgPosition = 0;
            userLeague.Rounds = [];
        }

        league.TotalPrize = 0;

        var playedUserRounds = await _dbContext.UserRounds
            .AsNoTracking()
            .Include(userRound => userRound.Round)
            .Where(userRound => userRound.Round.LeagueId == league.Id && userRound.Round.Status == Status.Played)
            .ToListAsync(cancellationToken);

        foreach (var userRound in playedUserRounds)
        {
            var userLeague = userLeagues.FirstOrDefault(ul => ul.UserId == userRound.UserId);
            if (userLeague is null)
            {
                continue;
            }

            userLeague.Score += (int)Math.Round(userRound.Score);
            userLeague.RoundsPlayed += 1;
            userLeague.Rounds.Add(userRound.Position);
            userLeague.BestRound = userLeague.BestRound == 0 || userRound.Position < userLeague.BestRound
                ? userRound.Position
                : userLeague.BestRound;
            userLeague.AvgPosition = userLeague.Rounds.Count > 0
                ? userLeague.Rounds.Average()
                : 0;
            league.TotalPrize += league.PrizePerPerson;
        }

        _dbContext.Update(league);
        _dbContext.UpdateRange(userLeagues);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class RoundResultEntry
    {
        public required UpdateRoundPlayerDto Player { get; init; }
        public int Index { get; init; }
        public int Score { get; init; }
    }

    public async Task UpdateLeagueResultsAsync(int leagueId, string userId, List<UserWithScore> userList, CancellationToken cancellationToken = default)
    {
        await EnsureUserAsync(userId);

        _logger.LogInformation(
            "UpdateLeagueResults start {LeagueId} {UserId} {UserCount}",
            leagueId,
            userId,
            userList.Count);

        var league = await _dbContext.Leagues
            .AsTracking()
            .FirstOrDefaultAsync(l => l.Id == leagueId, cancellationToken);
        if (league is null)
        {
            _logger.LogWarning("UpdateLeagueResults league not found {LeagueId}", leagueId);
            throw LeagueServiceException.NotFound("Errors.Leagues.NotFound");
        }

        if (!await IsLeagueAdminAsync(league, userId, cancellationToken))
        {
            _logger.LogWarning("UpdateLeagueResults unauthorized {LeagueId} {UserId}", leagueId, userId);
            throw LeagueServiceException.Unauthorized("Errors.Leagues.Unauthorized");
        }

        if (league.CurrentRound == 0)
        {
            _logger.LogWarning("UpdateLeagueResults invalid current round {LeagueId} {CurrentRound}", leagueId, league.CurrentRound);
            throw LeagueServiceException.BadRequest("Errors.Leagues.InvalidCurrentRound");
        }

        var round = await _dbContext.Rounds
            .AsTracking()
            .FirstOrDefaultAsync(r => r.LeagueId == league.Id && r.Id == league.CurrentRound, cancellationToken);
        if (round is null)
        {
            _logger.LogWarning(
                "UpdateLeagueResults round not found {LeagueId} {CurrentRound}",
                leagueId,
                league.CurrentRound);
            throw LeagueServiceException.NotFound("Errors.Leagues.NotFound");
        }

        var requestedUserIds = userList
            .Select(user => user.UserId)
            .ToList();
        if (requestedUserIds.Any(string.IsNullOrWhiteSpace))
        {
            _logger.LogWarning("UpdateLeagueResults invalid user ids {LeagueId}", leagueId);
            throw LeagueServiceException.BadRequest("Errors.Leagues.ValidationFailed");
        }

        var distinctUserIds = requestedUserIds
            .Distinct()
            .ToList();
        if (distinctUserIds.Count != requestedUserIds.Count)
        {
            _logger.LogWarning("UpdateLeagueResults duplicate user ids {LeagueId}", leagueId);
            throw LeagueServiceException.BadRequest("Errors.Leagues.ValidationFailed");
        }

        var userLeagues = await _dbContext.UserLeagues
            .Where(ul => ul.LeagueId == league.Id && ul.IsPlaying)
            .Include(ul => ul.User)
            .ToListAsync(cancellationToken);

        var validUserIds = userLeagues
            .Select(ul => ul.UserId)
            .ToHashSet();
        if (distinctUserIds.Any(userId2 => !validUserIds.Contains(userId2)))
        {
            _logger.LogWarning("UpdateLeagueResults non-member players {LeagueId}", leagueId);
            throw LeagueServiceException.BadRequest("Errors.Leagues.UserMustBeMember", includeBody: true);
        }

        var existingUserRounds = await _dbContext.UserRounds
            .Where(ur => ur.RoundId == round.Id)
            .ToListAsync(cancellationToken);
        _dbContext.UserRounds.RemoveRange(existingUserRounds);

        var orderedUsers = userList
            .Select((user, index) => new UserResultEntry
            {
                User = user,
                Index = index,
                Score = league.ScoringSystem == ScoringSystem.Victories
                    ? (user.Wins ?? 0) * (league.PointsPerWin ?? 0)
                      + (user.Draws ?? 0) * (league.PointsPerDraw ?? 0)
                      + (user.Losses ?? 0) * (league.PointsPerLoss ?? 0)
                    : 0
            })
            .ToList();

        if (league.ScoringSystem == ScoringSystem.Victories)
        {
            orderedUsers = orderedUsers
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.User.Omw)
                .ThenByDescending(x => x.User.Gw)
                .ThenByDescending(x => x.User.Ogw)
                .ThenBy(x => x.Index)
                .ToList();
        }

        var userRounds = new List<AppUserRound>();
        for (var index = 0; index < orderedUsers.Count; index++)
        {
            var userWithScore = orderedUsers[index].User;
            var userLeague = userLeagues.First(x => x.UserId == userWithScore.UserId);
            var position = index + 1;

            var roundScore = 0;
            if (league.ScoringSystem == ScoringSystem.Positional)
            {
                roundScore = league.PointsToGive != null && league.PointsToGive.Count > index
                    ? league.PointsToGive[index]
                    : 0;
            }

            if (league.ScoringSystem == ScoringSystem.Victories)
            {
                var wins = userWithScore.Wins ?? 0;
                var draws = userWithScore.Draws ?? 0;
                var losses = userWithScore.Losses ?? 0;
                var pointsPerWin = league.PointsPerWin ?? 0;
                var pointsPerDraw = league.PointsPerDraw ?? 0;
                var pointsPerLoss = league.PointsPerLoss ?? 0;
                roundScore = wins * pointsPerWin + draws * pointsPerDraw + losses * pointsPerLoss;
            }

            userLeague.Score += roundScore;
            userLeague.BestRound = userLeague.BestRound == 0 || position < userLeague.BestRound
                ? position
                : userLeague.BestRound;
            userLeague.RoundsPlayed += 1;
            userLeague.Rounds.Add(position);
            userLeague.AvgPosition = userLeague.Rounds.Average();
            league.TotalPrize += league.PrizePerPerson;

            userRounds.Add(new AppUserRound
            {
                UserId = userLeague.UserId,
                User = userLeague.User!,
                RoundId = round.Id,
                Round = round,
                Position = position,
                Score = roundScore,
                Wins = userWithScore.Wins ?? 0,
                Draws = userWithScore.Draws ?? 0,
                Losses = userWithScore.Losses ?? 0,
                Gw = userWithScore.Gw,
                Ogw = userWithScore.Ogw,
                Omw = userWithScore.Omw
            });
        }

        round.Players = userRounds;
        round.Status = Status.Played;

        var nextRound = await _dbContext.Rounds
            .AsNoTracking()
            .Where(r => r.LeagueId == league.Id && r.Order > round.Order)
            .OrderBy(r => r.Order)
            .FirstOrDefaultAsync(cancellationToken);
        if (nextRound != null)
        {
            league.CurrentRound = nextRound.Id;
        }

        _dbContext.Update(league);
        _dbContext.Update(round);
        _dbContext.UpdateRange(userLeagues);
        await _dbContext.UserRounds.AddRangeAsync(userRounds, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class UserResultEntry
    {
        public required UserWithScore User { get; init; }
        public int Index { get; init; }
        public int Score { get; init; }
    }

    public async Task<string> GetInviteCodeAsync(int leagueId, string userId, CancellationToken cancellationToken = default)
    {
        await EnsureUserAsync(userId);

        var league = await _dbContext.Leagues
            .AsTracking()
            .FirstOrDefaultAsync(l => l.Id == leagueId, cancellationToken);
        if (league is null)
        {
            throw LeagueServiceException.NotFound("Errors.Leagues.NotFound");
        }

        if (!await IsLeagueAdminAsync(league, userId, cancellationToken))
        {
            throw LeagueServiceException.Unauthorized("Errors.Leagues.Unauthorized");
        }

        return league.Code;
    }

    public async Task RequestJoinLeagueAsync(string code, string userId, CancellationToken cancellationToken = default)
    {
        var user = await EnsureUserAsync(userId);

        var league = await _dbContext.Leagues
            .FirstOrDefaultAsync(l => l.Code == code, cancellationToken);
        if (league is null)
        {
            throw LeagueServiceException.NotFound("Errors.Leagues.NotFound", includeBody: true);
        }

        var existingMember = await _dbContext.UserLeagues
            .AnyAsync(ul => ul.LeagueId == league.Id && ul.UserId == user.Id, cancellationToken);
        if (existingMember)
        {
            throw LeagueServiceException.BadRequest("Errors.Leagues.UserAlreadyJoined", includeBody: true);
        }

        var adminAssignments = await _dbContext.LeagueRoleAssignments
            .AsNoTracking()
            .Where(lr => lr.LeagueId == league.Id)
            .Select(lr => new { lr.UserId, lr.Roles })
            .ToListAsync(cancellationToken);

        var adminRecipients = adminAssignments
            .Where(a => a.Roles.HasFlag(LeagueRole.Admin))
            .Select(a => a.UserId)
            .ToList();

        if (!adminRecipients.Contains(league.OwnerId))
        {
            adminRecipients.Add(league.OwnerId);
        }

        foreach (var adminId in adminRecipients.Distinct().Where(id => id != user.Id))
        {
            var newNotification = new NewNotificationDto
            {
                Name = "request_join_league",
                Message = "Notifications.RequestJoinLeague",
                MessageKey = "Notifications.RequestJoinLeague",
                MessageArgs = [user.FirstName, user.LastName, league.Name],
                ObjectId = league.Id.ToString(),
                Origin = league.Id + "." + user.Id,
                AppUserId = adminId
            };

            await _notificationService.CreateNotificationAsync(newNotification);
        }
    }

    public async Task JoinLeagueAsync(int leagueId, string userId, CancellationToken cancellationToken = default)
    {
        var user = await EnsureUserAsync(userId);

        var league = await _dbContext.Leagues.FindAsync([leagueId], cancellationToken);
        if (league is null)
        {
            throw LeagueServiceException.NotFound("Errors.Leagues.NotFound", includeBody: true);
        }

        var existingMember = await _dbContext.UserLeagues
            .AnyAsync(ul => ul.LeagueId == league.Id && ul.UserId == user.Id, cancellationToken);
        if (existingMember)
        {
            throw LeagueServiceException.BadRequest("Errors.Leagues.UserAlreadyJoined", includeBody: true);
        }

        var exactMatch = $"{league.Id}.{userId}";

        var approved = await _dbContext.Notifications
            .AnyAsync(n =>
                n.Origin == exactMatch
                && n.Approval, cancellationToken);
        if (!approved)
        {
            throw LeagueServiceException.BadRequest("Errors.Leagues.UserNotApproved", includeBody: true);
        }

        await _dbContext.AddAsync(new AppUserLeague
        {
            UserId = user.Id,
            User = user,
            LeagueId = league.Id,
            League = league,
            Score = 0,
            RoundsPlayed = 0,
            Rounds = [],
            BestRound = 0,
            AvgPosition = 0
        }, cancellationToken);

        league.TotalPlayers++;
        _dbContext.Update(league);
        await AssignLeagueRoleAsync(user.Id, league.Id, LeagueRole.Player, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var newNotification = new NewNotificationDto
        {
            Name = "player_joined_league",
            Message = "Notifications.PlayerJoinedLeague",
            MessageKey = "Notifications.PlayerJoinedLeague",
            MessageArgs = [user.FirstName, user.LastName, league.Name],
            ObjectId = league.Id.ToString(),
            Origin = $"{league.Id}.{user.Id}",
            AppUserId = league.OwnerId
        };

        await _notificationService.CreateNotificationAsync(newNotification);
    }

    public async Task LeaveLeagueAsync(int leagueId, string userId, CancellationToken cancellationToken = default)
    {
        var user = await EnsureUserAsync(userId);

        var league = await _dbContext.Leagues
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == leagueId, cancellationToken);
        if (league is null)
        {
            throw LeagueServiceException.NotFound("Errors.Leagues.NotFound", includeBody: true);
        }

        if (league.OwnerId == user.Id)
        {
            throw LeagueServiceException.BadRequest("Errors.Leagues.CannotLeaveOwner", includeBody: true);
        }

        var res = _dbContext.UserLeagues.FirstOrDefault(ul => ul.LeagueId == league.Id && ul.UserId == userId);
        if (res is null)
        {
            throw LeagueServiceException.BadRequest("Errors.Leagues.UserNotPlaying", includeBody: true);
        }

        res.IsPlaying = false;
        _dbContext.Update(res);
        await RemoveLeagueRoleAsync(userId, league.Id, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var newNotification = new NewNotificationDto
        {
            Name = "user_leave_league",
            Message = "Notifications.UserLeftLeague",
            MessageKey = "Notifications.UserLeftLeague",
            MessageArgs = [user.FirstName, user.LastName, league.Name],
            ObjectId = league.Id.ToString(),
            Origin = league.Id + "." + user.Id,
            AppUserId = league.OwnerId
        };

        await _notificationService.CreateNotificationAsync(newNotification);
    }

    public async Task<League> CreateLeagueAsync(string userId, NewLeagueDto leagueDto, CancellationToken cancellationToken = default)
    {
        var user = await EnsureUserAsync(userId);

        var (isValid, errors) = _validationService.ValidateModel(leagueDto);
        if (!isValid)
        {
            throw LeagueServiceException.ValidationFailed(errors, "Errors.Leagues.ValidationFailed");
        }

        var league = new League
        {
            Name = leagueDto.Name,
            OwnerId = user.Id,
            Code = SecureCodeGenerator.GenerateCode(),
            Format = leagueDto.Format,
            TotalRounds = leagueDto.TotalRounds,
            RoundsToConsider = leagueDto.RoundsToConsider,
            MinimumRounds = leagueDto.MinimumRounds,
            TotalPrize = leagueDto.TotalPrize ?? 0,
            PrizePerPerson = leagueDto.PrizePerPerson,
            TotalPlayers = 0,
            PointsToGive = leagueDto.PointsToGive ?? null,
            IsActive = true,
            IsPublic = leagueDto.IsPublic,
            ScoringSystem = leagueDto.ScoringSystem,
            PointsPerWin = leagueDto.PointsPerWin ?? null,
            PointsPerDraw = leagueDto.PointsPerDraw ?? null,
            PointsPerLoss = leagueDto.PointsPerLoss ?? null
        };

        await _dbContext.AddAsync(league, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (league.TotalRounds > 0)
        {
            var rounds = Enumerable.Range(1, league.TotalRounds)
                .Select(order => new Round
                {
                    LeagueId = league.Id,
                    League = league,
                    Order = order
                })
                .ToList();

            await _dbContext.AddRangeAsync(rounds, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            league.CurrentRound = rounds[0].Id;
            _dbContext.Update(league);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await AssignLeagueRoleAsync(user.Id, league.Id, LeagueRole.Admin, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var userLeague = new AppUserLeague
        {
            UserId = userId,
            LeagueId = league.Id,
            Score = 0,
            RoundsPlayed = 0,
            Rounds = [],
            BestRound = 0,
            AvgPosition = 0,
            IsPlaying = false
        };

        await _dbContext.AddAsync(userLeague, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return league;
    }

    public async Task JoinAsPlayerAsync(int leagueId, string userId, CancellationToken cancellationToken = default)
    {
        var user = await EnsureUserAsync(userId);

        var league = await _dbContext.Set<League>()
            .Where(x => x.Id == leagueId)
            .FirstOrDefaultAsync(cancellationToken);
        if (league is null)
        {
            throw LeagueServiceException.NotFound("Errors.Leagues.NotFound", includeBody: true);
        }

        if (league.OwnerId != userId)
        {
            throw LeagueServiceException.Unauthorized("Errors.Leagues.Unauthorized");
        }

        if (!league.IsActive)
        {
            throw LeagueServiceException.BadRequest("Errors.Leagues.LeagueInactive");
        }

        var ownerMembership = await _dbContext.UserLeagues
            .FirstOrDefaultAsync(ul => ul.LeagueId == league.Id && ul.UserId == user.Id, cancellationToken);
        if (ownerMembership is null)
        {
            throw LeagueServiceException.BadRequest("Errors.Leagues.OwnerMembershipNotFound", includeBody: true);
        }

        if (ownerMembership.IsPlaying)
        {
            throw LeagueServiceException.BadRequest("Errors.Leagues.OwnerAlreadyPlayer", includeBody: true);
        }

        ownerMembership.IsPlaying = true;
        _dbContext.UserLeagues.Update(ownerMembership);

        league.TotalPlayers++;
        _dbContext.Update(league);
        await AssignLeagueRoleAsync(user.Id, league.Id, LeagueRole.Player, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task PromoteLeagueAdminAsync(int leagueId, string userId, string targetUserId, CancellationToken cancellationToken = default)
    {
        var caller = await EnsureUserAsync(userId);

        var league = await _dbContext.Leagues
            .AsTracking()
            .FirstOrDefaultAsync(l => l.Id == leagueId, cancellationToken);
        if (league is null)
        {
            throw LeagueServiceException.NotFound("Errors.Leagues.NotFound");
        }

        if (league.OwnerId != caller.Id)
        {
            throw LeagueServiceException.Unauthorized("Errors.Leagues.CallerNotOwner");
        }

        var targetUser = await _userManager.FindByIdAsync(targetUserId);
        if (targetUser is null)
        {
            throw LeagueServiceException.NotFound("Errors.Leagues.UserNotFound", includeBody: true);
        }

        var isMember = await _dbContext.UserLeagues
            .AsNoTracking()
            .AnyAsync(ul => ul.LeagueId == league.Id && ul.UserId == targetUser.Id && ul.IsPlaying, cancellationToken);
        if (!isMember)
        {
            throw LeagueServiceException.BadRequest("Errors.Leagues.UserMustBeMember", includeBody: true);
        }

        await AssignLeagueRoleAsync(targetUser.Id, league.Id, LeagueRole.Admin, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
    
    public async Task TerminateLeagueAsync(int leagueId, string userId, CancellationToken cancellationToken = default)
    {
        var caller = await EnsureUserAsync(userId);

        var league = await _dbContext.Leagues
            .AsTracking()
            .FirstOrDefaultAsync(l => l.Id == leagueId, cancellationToken);
        if (league is null)
        {
            throw LeagueServiceException.NotFound("Errors.Leagues.NotFound");
        }

        if (league.OwnerId != caller.Id)
        {
            throw LeagueServiceException.Unauthorized("Errors.Leagues.CallerNotOwner");
        }

        league.IsActive = false;

        _dbContext.Update(league);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<AppUser> EnsureUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw LeagueServiceException.Unauthorized("Errors.Leagues.MissingUserId");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            throw LeagueServiceException.Unauthorized("Errors.Leagues.UserNotFound");
        }

        return user;
    }

    private async Task AssignLeagueRoleAsync(string userId, int leagueId, LeagueRole role, CancellationToken cancellationToken)
    {
        var userExists = await _dbContext.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!userExists)
        {
            return;
        }

        var assignment = await _dbContext.LeagueRoleAssignments
            .FirstOrDefaultAsync(x => x.LeagueId == leagueId && x.UserId == userId, cancellationToken);

        if (assignment is null)
        {
            await _dbContext.LeagueRoleAssignments.AddAsync(new LeagueRoleAssignment
            {
                LeagueId = leagueId,
                UserId = userId,
                Roles = role
            }, cancellationToken);
            return;
        }

        if (!assignment.Roles.HasFlag(role))
        {
            assignment.Roles |= role;
            _dbContext.LeagueRoleAssignments.Update(assignment);
        }
    }

    private async Task RemoveLeagueRoleAsync(string userId, int leagueId, CancellationToken cancellationToken)
    {
        var assignment = await _dbContext.LeagueRoleAssignments
            .FirstOrDefaultAsync(x => x.LeagueId == leagueId && x.UserId == userId, cancellationToken);

        if (assignment != null)
        {
            _dbContext.LeagueRoleAssignments.Remove(assignment);
        }
    }

    private async Task<bool> IsLeagueAdminAsync(League league, string userId, CancellationToken cancellationToken)
    {
        if (league.OwnerId == userId) return true;

        var assignment = await _dbContext.LeagueRoleAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.LeagueId == league.Id && x.UserId == userId, cancellationToken);

        return assignment?.Roles.HasFlag(LeagueRole.Admin) == true;
    }

    private static class SecureCodeGenerator
    {
        private static readonly char[] Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

        public static string GenerateCode(int length = 6)
        {
            var data = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(data);
            }

            var result = new StringBuilder(length);
            foreach (var b in data)
            {
                result.Append(Chars[b % Chars.Length]);
            }

            return result.ToString();
        }
    }
}

public sealed class LeagueServiceException : Exception
{
    public int StatusCode { get; }
    public object? Body { get; }
    public bool IncludeBody { get; }

    private LeagueServiceException(int statusCode, string? message, object? body, bool includeBody)
        : base(message ?? string.Empty)
    {
        StatusCode = statusCode;
        Body = body;
        IncludeBody = includeBody;
    }

    public static LeagueServiceException Unauthorized(string message)
        => new(StatusCodes.Status401Unauthorized, message, null, false);

    public static LeagueServiceException NotFound(string message)
        => new(StatusCodes.Status404NotFound, message, null, false);

    public static LeagueServiceException NotFound(string message, bool includeBody)
        => new(StatusCodes.Status404NotFound, message, message, includeBody);

    public static LeagueServiceException BadRequest(string message)
        => new(StatusCodes.Status400BadRequest, message, null, false);

    public static LeagueServiceException BadRequest(string message, bool includeBody)
        => new(StatusCodes.Status400BadRequest, message, message, includeBody);

    public static LeagueServiceException ValidationFailed(Dictionary<string, string[]> errors, string message)
        => new(StatusCodes.Status400BadRequest, message, new ValidationErrorsResponse(errors), true);
}

public sealed class ValidationErrorsResponse
{
    public ValidationErrorsResponse(Dictionary<string, string[]> errors)
    {
        Errors = errors;
    }

    public Dictionary<string, string[]> Errors { get; }
}
