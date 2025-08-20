using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TestUtilities.Authentication;

/// <summary>
/// Custom authentication handler that bypasses JWT validation in tests.
/// Creates authenticated users based on test configuration.
/// </summary>
public class TestAuthenticationHandler : AuthenticationHandler<TestAuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(
        IOptionsMonitor<TestAuthenticationSchemeOptions> options,
        ILoggerFactory logger, 
        UrlEncoder encoder, 
        ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    /// <summary>
    /// Always succeeds authentication with the configured test user.
    /// Creates the same claims structure as your real JWT tokens.
    /// </summary>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Create claims that match your JWT token structure
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Options.UserId),
            new Claim(ClaimTypes.Name, Options.UserName),
            new Claim(ClaimTypes.Email, Options.Email),
            // Add additional claims as needed to match your JWT structure
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new Claim("jti", Guid.NewGuid().ToString())
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        // Log authentication for debugging
        Logger.LogInformation("Test authentication for user {UserId} ({UserName})", 
            Options.UserId, Options.UserName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}