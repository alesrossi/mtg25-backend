using API.Constants;
using API.Dtos.Notifications;
using API.Dtos.Teams;
using Core.Enums;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Services;

public interface ITeamService
{
    Task<IReadOnlyList<TeamDto>> GetTeamsForUserAsync(string userId);
    Task<TeamDto> GetTeamByIdAsync(int teamId, string userId);
    Task<TeamDto> CreateTeamAsync(CreateTeamDto dto, string userId);
    Task<TeamDto> UpdateTeamAsync(int teamId, UpdateTeamDto dto, string userId);
    Task DeleteTeamAsync(int teamId, string userId);
    Task InviteMemberAsync(int teamId, string inviterUserId, string targetUserId);
    Task AcceptInviteAsync(int teamId, string userId);
    Task RejectInviteAsync(int teamId, string userId);
    Task RemoveMemberAsync(int teamId, string adminUserId, string targetUserId);
    Task UpdateMemberRoleAsync(int teamId, string adminUserId, string targetUserId, TeamRole newRole);
    Task<IReadOnlyList<TeamMemberDto>> GetTeamMembersAsync(int teamId, string requestingUserId);
    Task<bool> HasTeamAccessAsync(int teamId, string userId, TeamRole requiredRole);
}

public sealed class TeamService : ITeamService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<AppUser> _userManager;
    private readonly NotificationService _notificationService;

    public TeamService(
        IUnitOfWork unitOfWork,
        UserManager<AppUser> userManager,
        NotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _notificationService = notificationService;
    }

    public async Task<IReadOnlyList<TeamDto>> GetTeamsForUserAsync(string userId)
    {
        var memberRepo = _unitOfWork.CompositeRepository<TeamMember>();
        var teamRepo = _unitOfWork.Repository<Team>();

        var memberships = await memberRepo.Query
            .Where(m => m.UserId == userId)
            .Join(
                teamRepo.Query,
                m => m.TeamId,
                t => t.Id,
                (m, t) => new { Membership = m, Team = t })
            .ToListAsync();

        var result = new List<TeamDto>();

        foreach (var item in memberships)
        {
            var owner = await _userManager.FindByIdAsync(item.Team.OwnerId);
            var memberCount = await memberRepo.Query.CountAsync(m => m.TeamId == item.Team.Id);

            result.Add(new TeamDto
            {
                Id = item.Team.Id,
                Name = item.Team.Name,
                Description = item.Team.Description,
                OwnerId = item.Team.OwnerId,
                OwnerDisplayName = owner?.UserName ?? owner?.Email ?? "Unknown",
                CreatedAt = item.Team.CreatedAt,
                MemberCount = memberCount,
                UserRole = item.Membership.Role
            });
        }

        return result;
    }

    public async Task<TeamDto> GetTeamByIdAsync(int teamId, string userId)
    {
        var team = await _unitOfWork.Repository<Team>().Query
            .FirstOrDefaultAsync(t => t.Id == teamId);

        if (team is null)
            throw TeamServiceException.Problem(404, "Not Found", "Team not found.");

        if (!await HasTeamAccessAsync(teamId, userId, TeamRole.Member))
            throw TeamServiceException.Problem(403, "Forbidden", "You do not have access to this team.");

        var membership = await _unitOfWork.CompositeRepository<TeamMember>().Query
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId);

        var owner = await _userManager.FindByIdAsync(team.OwnerId);
        var memberCount = await _unitOfWork.CompositeRepository<TeamMember>().Query
            .CountAsync(m => m.TeamId == teamId);

        return new TeamDto
        {
            Id = team.Id,
            Name = team.Name,
            Description = team.Description,
            OwnerId = team.OwnerId,
            OwnerDisplayName = owner?.UserName ?? owner?.Email ?? "Unknown",
            CreatedAt = team.CreatedAt,
            MemberCount = memberCount,
            UserRole = membership?.Role
        };
    }

    public async Task<TeamDto> CreateTeamAsync(CreateTeamDto dto, string userId)
    {
        var team = new Team
        {
            Name = dto.Name,
            Description = dto.Description,
            OwnerId = userId
        };

        _unitOfWork.Repository<Team>().Add(team);
        await _unitOfWork.Complete();

        var ownerMember = new TeamMember
        {
            UserId = userId,
            TeamId = team.Id,
            Role = TeamRole.Owner,
            JoinedAt = DateTime.UtcNow
        };

        _unitOfWork.CompositeRepository<TeamMember>().Add(ownerMember);
        await _unitOfWork.Complete();

        return await GetTeamByIdAsync(team.Id, userId);
    }

    public async Task<TeamDto> UpdateTeamAsync(int teamId, UpdateTeamDto dto, string userId)
    {
        if (!await HasTeamAccessAsync(teamId, userId, TeamRole.Owner))
            throw TeamServiceException.Problem(403, "Forbidden", "Owner access required.");

        var team = await _unitOfWork.Repository<Team>().Query
            .FirstOrDefaultAsync(t => t.Id == teamId);

        if (team is null)
            throw TeamServiceException.Problem(404, "Not Found", "Team not found.");

        if (dto.Name is not null)
            team.Name = dto.Name;

        if (dto.Description is not null)
            team.Description = dto.Description;

        await _unitOfWork.Complete();

        return await GetTeamByIdAsync(teamId, userId);
    }

    public async Task DeleteTeamAsync(int teamId, string userId)
    {
        if (!await HasTeamAccessAsync(teamId, userId, TeamRole.Owner))
            throw TeamServiceException.Problem(403, "Forbidden", "Only the owner can delete the team.");

        var team = await _unitOfWork.Repository<Team>().Query
            .FirstOrDefaultAsync(t => t.Id == teamId);

        if (team is null)
            throw TeamServiceException.Problem(404, "Not Found", "Team not found.");

        var members = await _unitOfWork.CompositeRepository<TeamMember>().Query
            .Where(m => m.TeamId == teamId)
            .ToListAsync();

        if (members.Count > 0)
            _unitOfWork.CompositeRepository<TeamMember>().RemoveRange(members);

        _unitOfWork.Repository<Team>().Delete(team);
        await _unitOfWork.Complete();
    }

    public async Task InviteMemberAsync(int teamId, string inviterUserId, string targetUserId)
    {
        if (!await HasTeamAccessAsync(teamId, inviterUserId, TeamRole.Admin))
            throw TeamServiceException.Problem(403, "Forbidden", "Admin access required to invite members.");

        var existing = await _unitOfWork.CompositeRepository<TeamMember>().Query
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == targetUserId);

        if (existing is not null)
            throw TeamServiceException.Problem(409, "Conflict", "User is already a member of this team.");

        var member = new TeamMember
        {
            UserId = targetUserId,
            TeamId = teamId,
            Role = TeamRole.Member,
            InvitedById = inviterUserId,
            JoinedAt = DateTime.UtcNow
        };

        _unitOfWork.CompositeRepository<TeamMember>().Add(member);
        await _unitOfWork.Complete();

        await _notificationService.CreateNotificationAsync(new NewNotificationDto
        {
            Name = NotificationConstants.TeamInviteSent,
            Message = NotificationConstants.TeamInviteSent,
            MessageKey = NotificationConstants.TeamInviteSent,
            Origin = $"{inviterUserId}.{targetUserId}",
            ObjectId = teamId.ToString(),
            AppUserId = targetUserId
        });
    }

    public async Task AcceptInviteAsync(int teamId, string userId)
    {
        var member = await _unitOfWork.CompositeRepository<TeamMember>().Query
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId);

        if (member is null)
            throw TeamServiceException.Problem(404, "Not Found", "Invite not found.");

        member.JoinedAt = DateTime.UtcNow;
        await _unitOfWork.Complete();

        if (!string.IsNullOrEmpty(member.InvitedById))
        {
            await _notificationService.CreateNotificationAsync(new NewNotificationDto
            {
                Name = NotificationConstants.TeamInviteAccepted,
                Message = NotificationConstants.TeamInviteAccepted,
                MessageKey = NotificationConstants.TeamInviteAccepted,
                Origin = $"{userId}.{member.InvitedById}",
                ObjectId = teamId.ToString(),
                AppUserId = member.InvitedById
            });
        }
    }

    public async Task RejectInviteAsync(int teamId, string userId)
    {
        var member = await _unitOfWork.CompositeRepository<TeamMember>().Query
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId);

        if (member is null)
            throw TeamServiceException.Problem(404, "Not Found", "Invite not found.");

        var inviterId = member.InvitedById;

        _unitOfWork.CompositeRepository<TeamMember>().RemoveRange(new[] { member });
        await _unitOfWork.Complete();

        if (!string.IsNullOrEmpty(inviterId))
        {
            await _notificationService.CreateNotificationAsync(new NewNotificationDto
            {
                Name = NotificationConstants.TeamInviteRejected,
                Message = NotificationConstants.TeamInviteRejected,
                MessageKey = NotificationConstants.TeamInviteRejected,
                Origin = $"{userId}.{inviterId}",
                ObjectId = teamId.ToString(),
                AppUserId = inviterId
            });
        }
    }

    public async Task RemoveMemberAsync(int teamId, string adminUserId, string targetUserId)
    {
        if (!await HasTeamAccessAsync(teamId, adminUserId, TeamRole.Admin))
            throw TeamServiceException.Problem(403, "Forbidden", "Admin access required.");

        if (adminUserId == targetUserId)
            throw TeamServiceException.Problem(400, "Bad Request", "You cannot remove yourself.");

        var targetMember = await _unitOfWork.CompositeRepository<TeamMember>().Query
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == targetUserId);

        if (targetMember is null)
            throw TeamServiceException.Problem(404, "Not Found", "Member not found.");

        if (targetMember.Role.HasFlag(TeamRole.Owner))
            throw TeamServiceException.Problem(403, "Forbidden", "Owner cannot be removed.");

        _unitOfWork.CompositeRepository<TeamMember>().RemoveRange(new[] { targetMember });
        await _unitOfWork.Complete();

        await _notificationService.CreateNotificationAsync(new NewNotificationDto
        {
            Name = NotificationConstants.TeamMemberRemoved,
            Message = NotificationConstants.TeamMemberRemoved,
            MessageKey = NotificationConstants.TeamMemberRemoved,
            Origin = $"{adminUserId}.{targetUserId}",
            ObjectId = teamId.ToString(),
            AppUserId = targetUserId
        });
    }

    public async Task UpdateMemberRoleAsync(
        int teamId,
        string adminUserId,
        string targetUserId,
        TeamRole newRole)
    {
        if (!await HasTeamAccessAsync(teamId, adminUserId, TeamRole.Admin))
            throw TeamServiceException.Problem(403, "Forbidden", "Admin access required.");

        var targetMember = await _unitOfWork.CompositeRepository<TeamMember>().Query
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == targetUserId);

        if (targetMember is null)
            throw TeamServiceException.Problem(404, "Not Found", "Member not found.");

        if (targetMember.Role.HasFlag(TeamRole.Owner))
            throw TeamServiceException.Problem(403, "Forbidden", "Owner cannot be demoted.");

        var adminMember = await _unitOfWork.CompositeRepository<TeamMember>().Query
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == adminUserId);

        if (adminMember is not null && !adminMember.Role.HasFlag(TeamRole.Owner) && newRole.HasFlag(TeamRole.Owner))
            throw TeamServiceException.Problem(403, "Forbidden", "Only the owner can assign the owner role.");

        targetMember.Role = newRole;
        await _unitOfWork.Complete();

        await _notificationService.CreateNotificationAsync(new NewNotificationDto
        {
            Name = NotificationConstants.TeamRoleUpdated,
            Message = NotificationConstants.TeamRoleUpdated,
            MessageKey = NotificationConstants.TeamRoleUpdated,
            Origin = $"{adminUserId}.{targetUserId}",
            ObjectId = teamId.ToString(),
            AppUserId = targetUserId
        });
    }

    public async Task<IReadOnlyList<TeamMemberDto>> GetTeamMembersAsync(int teamId, string requestingUserId)
    {
        if (!await HasTeamAccessAsync(teamId, requestingUserId, TeamRole.Member))
            throw TeamServiceException.Problem(403, "Forbidden", "You do not have access to this team.");

        var members = await _unitOfWork.CompositeRepository<TeamMember>().Query
            .Where(m => m.TeamId == teamId)
            .ToListAsync();

        var result = new List<TeamMemberDto>();

        foreach (var member in members)
        {
            var user = await _userManager.FindByIdAsync(member.UserId);
            var invitedBy = member.InvitedById is not null
                ? await _userManager.FindByIdAsync(member.InvitedById)
                : null;

            result.Add(new TeamMemberDto
            {
                UserId = member.UserId,
                DisplayName = user?.UserName ?? "Unknown",
                Email = user?.Email ?? string.Empty,
                Role = member.Role,
                JoinedAt = member.JoinedAt,
                InvitedByDisplayName = invitedBy?.UserName
            });
        }

        return result;
    }

    public async Task<bool> HasTeamAccessAsync(int teamId, string userId, TeamRole requiredRole)
    {
        var member = await _unitOfWork.CompositeRepository<TeamMember>().Query
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId);

        if (member is null)
            return false;

        var userRole = member.Role;

        if (requiredRole == TeamRole.Member)
            return true;

        if (requiredRole == TeamRole.Admin)
            return userRole.HasFlag(TeamRole.Admin) || userRole.HasFlag(TeamRole.Owner);

        if (requiredRole == TeamRole.Owner)
            return userRole.HasFlag(TeamRole.Owner);

        return false;
    }
}

public sealed class TeamServiceException : Exception
{
    public TeamServiceException(string message) : base(message) { }

    public static TeamServiceException Problem(int statusCode, string title, string detail)
        => new TeamServiceException($"{title}: {detail}")
        {
            StatusCode = statusCode,
            Title = title,
            Detail = detail
        };

    public int StatusCode { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Detail { get; private set; } = string.Empty;
}
