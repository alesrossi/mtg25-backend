using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Core.Models.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace API.Services;

public class JwtService(IOptions<JwtSettings> jwtSettings, IDistributedCache cache)
    : IJwtService
{
    private readonly JwtSettings _jwtSettings = jwtSettings.Value;
    private const string RefreshTokenPrefix = "refresh_token:";
    private const string RefreshTokenUserPrefix = "refresh_user:";

    public Task<string> GenerateTokenAsync(AppUser user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);
    
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return Task.FromResult(tokenHandler.WriteToken(token));
    }


    public async Task<bool> IsTokenBlacklistedAsync(string token)
    {
        var key = $"blacklist_{token}";
        var result = await cache.GetStringAsync(key);
        return !string.IsNullOrEmpty(result);
    }

    public async Task BlacklistTokenAsync(string token, TimeSpan expiry)
    {
        var key = $"blacklist_{token}";
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry
        };
        await cache.SetStringAsync(key, "blacklisted", options);
    }

    public async Task<string?> ValidateTokenAsync(string token)
    {
        try
        {
            // Check if token is blacklisted
            if (await IsTokenBlacklistedAsync(token))
                return null;

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userId = jwtToken.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value;
            
            return userId;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetUserIdFromTokenAsync(string token)
    {
        return await ValidateTokenAsync(token);
    }

    public async Task<string> GenerateRefreshTokenAsync(string userId)
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(_jwtSettings.RefreshTokenExpiryDays)
        };

        var previousToken = await cache.GetStringAsync(GetRefreshUserKey(userId));
        if (!string.IsNullOrWhiteSpace(previousToken))
        {
            await cache.RemoveAsync(GetRefreshTokenKey(previousToken));
        }

        await cache.SetStringAsync(GetRefreshTokenKey(token), userId, options);
        await cache.SetStringAsync(GetRefreshUserKey(userId), token, options);

        return token;
    }

    public async Task<string?> ValidateRefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var userId = await cache.GetStringAsync(GetRefreshTokenKey(refreshToken));
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var currentToken = await cache.GetStringAsync(GetRefreshUserKey(userId));
        return string.Equals(currentToken, refreshToken, StringComparison.Ordinal) ? userId : null;
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, string? userId = null)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        await cache.RemoveAsync(GetRefreshTokenKey(refreshToken));

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var currentToken = await cache.GetStringAsync(GetRefreshUserKey(userId));
            if (string.Equals(currentToken, refreshToken, StringComparison.Ordinal))
            {
                await cache.RemoveAsync(GetRefreshUserKey(userId));
            }
        }
    }

    private static string GetRefreshTokenKey(string token) => $"{RefreshTokenPrefix}{token}";

    private static string GetRefreshUserKey(string userId) => $"{RefreshTokenUserPrefix}{userId}";
}
