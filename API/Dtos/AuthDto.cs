namespace API.Dtos;

public class AuthDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public string UserId { get; set; } = string.Empty;
}