using System.Security.Cryptography;
using System.Text;
using API.Logging;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;

namespace API.Endpoints.Leagues;

public static partial class LeaguesEndpoint
{
    private static class SecureCodeGenerator
    {
        private static readonly char[] Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();

        public static string GenerateCode(int length = 6)
        {
            var data = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(data);
            }

            var result = new StringBuilder(length);
            foreach (var b in data)
            {
                result.Append(Chars[b % Chars.Length]);
            }

            return result.ToString();
        }
    }
    
    private static async Task<bool> EnsureRoleAsync(
        UserManager<AppUser> userManager,
        AppUser user,
        string role,
        ILogger<LeaguesEndpointLogCategory> logger,
        string operation)
    {
        if (await userManager.IsInRoleAsync(user, role))
        {
            return true;
        }

        var result = await userManager.AddToRoleAsync(user, role);
        if (result.Succeeded)
        {
            return true;
        }

        logger.LogOperationWarning(operation, "Role assignment failed", new
        {
            userId = user.Id,
            role,
            Errors = result.Errors.Select(e => new { e.Code, e.Description })
        });

        return false;
    }
}
