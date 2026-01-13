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
    Task<League> UpdateLeagueAsync(int leagueId, string userId, UpdateLeagueDto updateLeague, CancellationToken cancellationToken = default);
    Task UpdateLeagueResultsAsync(int leagueId, string userId, List<UserWithScore> userList, CancellationToken cancellationToken = default);
    Task<string> GetInviteCodeAsync(int leagueId, string userId, CancellationToken cancellationToken = default);
    Task RequestJoinLeagueAsync(string code, string userId, CancellationToken cancellationToken = default);
    Task JoinLeagueAsync(int leagueId, string userId, CancellationToken cancellationToken = default);
    Task LeaveLeagueAsync(int leagueId, string userId, CancellationToken cancellationToken = default);
    Task<League> CreateLeagueAsync(string userId, NewLeagueDto leagueDto, CancellationToken cancellationToken = default);
    Task JoinAsPlayerAsync(int leagueId, string userId, CancellationToken cancellationToken = default);
    Task PromoteLeagueAdminAsync(int leagueId, string userId, string targetUserId, CancellationToken cancellationToken = default);
}

public sealed class LeagueService : ILeagueService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppIdentityDbContext _dbContext;
    private readonly IValidationService _validationService;
    private readonly NotificationService _notificationService;

    public LeagueService(
        UserManager<AppUser> userManager,
        AppIdentityDbContext dbContext,
        IValidationService validationService,
        NotificationService notificationService)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _validationService = validationService;
        _notificationService = notificationService;
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
                Code = x.League.Code,
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
            CurrentRound = league.CurrentRound,
            OwnerId = league.OwnerId,
            IsPublic = league.IsPublic
        };

        var orderedPlayers = res
            .Where(x => x.LeagueId == league.Id && x.IsPlaying)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.User.FirstName)
            .ThenBy(x => x.User.LastName);

        foreach (var player in orderedPlayers)
        {
            leagueWithScores.Scores.Add(new Score
            {
                FirstName = player.User.FirstName,
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
        if (updateLeague.CurrentRound == null || updateLeague.CurrentRound > league.TotalRounds)
        {
            throw LeagueServiceException.BadRequest("Errors.Leagues.InvalidCurrentRound");
        }

        league.CurrentRound = (int)updateLeague.CurrentRound;

        _dbContext.Update(league);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return league;
    }

    public async Task UpdateLeagueResultsAsync(int leagueId, string userId, List<UserWithScore> userList, CancellationToken cancellationToken = default)
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

        var res = await _dbContext.UserLeagues
            .Where(ul => ul.LeagueId == league.Id)
            .Include(ul => ul.User)
            .ToListAsync(cancellationToken);

        var count = 0;
        foreach (var userWithScore in userList)
        {
            var userLeague = res.FirstOrDefault(x => x.UserId == userWithScore.UserId)!;
            if (league.ScoringSystem == ScoringSystem.Positional)
            {
                userLeague.Score += league.PointsToGive!.Count <= count ? 0 : league.PointsToGive![count];
                userLeague.BestRound = userLeague.BestRound == 0 || count + 1 < userLeague.BestRound ? count + 1 : userLeague.BestRound;
                userLeague.RoundsPlayed += 1;
                userLeague.Rounds.Add(count + 1);
                userLeague.AvgPosition = userLeague.Rounds.Average();
                league.TotalPrize += league.PrizePerPerson;
            }

            if (league.ScoringSystem == ScoringSystem.Victories)
            {
                userLeague.Score += (int)(userWithScore.Wins! * league.PointsPerWin! + userWithScore.Draws! * league.PointsPerDraw! + userWithScore.Losses! * league.PointsPerLoss!);
                userLeague.BestRound = userLeague.BestRound == 0 || count + 1 < userLeague.BestRound ? count + 1 : userLeague.BestRound;
                userLeague.RoundsPlayed += 1;
                userLeague.Rounds.Add(count + 1);
                userLeague.AvgPosition = userLeague.Rounds.Average();
                league.TotalPrize += league.PrizePerPerson;
            }

            count++;
        }

        if (league.CurrentRound + 1 <= league.TotalRounds) league.CurrentRound++;
        _dbContext.Update(league);
        _dbContext.UpdateRange(res);
        await _dbContext.SaveChangesAsync(cancellationToken);
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
                MessageArgs = new[] { user.FirstName, user.LastName, league.Name },
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

        var league = await _dbContext.Leagues.FindAsync(new object?[] { leagueId }, cancellationToken);
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
            MessageArgs = new[] { user.FirstName, user.LastName, league.Name },
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
            MessageArgs = new[] { user.FirstName, user.LastName, league.Name },
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
