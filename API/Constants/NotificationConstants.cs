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
}
