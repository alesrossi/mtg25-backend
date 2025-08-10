using Core.Models.Identity;

namespace API.Services;

public interface IJwtService
{
    Task<string> GenerateTokenAsync(AppUser user);
    Task<bool> IsTokenBlacklistedAsync(string token);
    Task BlacklistTokenAsync(string token, TimeSpan expiry);
    Task<string?> ValidateTokenAsync(string token);
    Task<string?> GetUserIdFromTokenAsync(string token);
}