// TestUtilities/JwtTokenHelper.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TestUtilities
{
    public static class JwtTokenHelper
    {
        private const string SecretKey = "A6e48opuUdbWcyLLHIwbeKVo826sEYYgepR9XtedHfRqhJ5drn7Nl85Yvb6WHsaYmp22gOXAsCOQvFzbxxqI3FQ2DUuf3C07Z50EaINValFi4FuuPX1pptcBHrzvl4qp";
        private const string Issuer = "MTG25API";
        private const string Audience = "MTG25Users";

        public static string GenerateToken(string userId, string userName, string email, int expiryMinutes = 60)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userName),
                new Claim(ClaimTypes.Email, email),
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}