namespace API.Constants;

/// <summary>
/// Constants for notification names used throughout the application.
/// Made public to allow access from unit and integration tests.
/// </summary>
public static class NotificationConstants
{
    public const string TradeRequest = "trade_request";
    public const string TradeCommitRequest = "trade_commit_request";
    public const string FriendRequest = "friend_request";
    public const string RequestJoinLeague = "request_join_league";
    public const string JoinedLeague = "joined_league";
    public const string PromotedToLeagueAdmin = "promoted_to_league_admin";

    // Team notifications
    public const string TeamInviteSent = "team_invite_sent";
    public const string TeamInviteAccepted = "team_invite_accepted";
    public const string TeamInviteRejected = "team_invite_rejected";
    public const string TeamMemberRemoved = "team_member_removed";
    public const string TeamRoleUpdated = "team_role_updated";
}
