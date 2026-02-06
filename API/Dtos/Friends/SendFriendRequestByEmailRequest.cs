using System.ComponentModel.DataAnnotations;

namespace API.Dtos.Friends;

public sealed class SendFriendRequestByEmailRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public required string Email { get; set; }
}
