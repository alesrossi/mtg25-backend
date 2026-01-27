using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TestUtilities.Builders;

namespace TestUtilities.Authentication;

public static class TestUserFactory
{
    private const string DefaultPassword = "Password123!";

    public static async Task<AppUser> CreateAsync(
        IServiceProvider services,
        TestDataBuilder builder,
        string baseEmail,
        string baseUserName,
        bool requirePassword = false,
        string? password = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(builder);

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var (uniqueEmail, uniqueUserName) = GenerateUniqueIdentifiers(baseEmail, baseUserName);
        var user = builder.CreateUser(uniqueEmail, uniqueUserName);
        user.UserLeagues = new List<AppUserLeague>();
        user.LeagueRoles = new List<LeagueRoleAssignment>();

        var result = requirePassword
            ? await userManager.CreateAsync(user, password ?? DefaultPassword)
            : await userManager.CreateAsync(user);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create test user: {errors}");
        }

        return user;
    }

    private static (string Email, string UserName) GenerateUniqueIdentifiers(string email, string userName)
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];

        string resultEmail;
        if (string.IsNullOrWhiteSpace(email))
        {
            resultEmail = $"user_{uniqueId}@example.com";
        }
        else if (email.Contains('@'))
        {
            var at = email.IndexOf('@');
            var local = email[..at];
            var domain = email[(at + 1)..];
            resultEmail = $"{local}_{uniqueId}@{domain}";
        }
        else
        {
            resultEmail = $"{email}_{uniqueId}@example.com";
        }

        var resultUserName = string.IsNullOrWhiteSpace(userName)
            ? $"user_{uniqueId}"
            : $"{userName}_{uniqueId}";

        return (resultEmail, resultUserName);
    }
}
