namespace API.Dtos.Accounts;

public class GoogleLoginDto
{
    public required string IdToken { get; set; }
    public bool IncludeRefreshToken { get; set; }
}
