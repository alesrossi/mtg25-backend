using System;
using Core.Enums;

namespace API.Dtos.Friends;

public sealed class FriendDto
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public FriendshipStatus Status { get; set; }
    public bool IsRequester { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}
