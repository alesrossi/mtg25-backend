namespace API.Dtos.Accounts;

public class AuthDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public string? RefreshToken { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FirstName {get; set;} = string.Empty;
    public string LastName {get; set;} = string.Empty;
    public string DisplayName {get; set;} = string.Empty;
}
