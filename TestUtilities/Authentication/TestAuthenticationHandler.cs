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
        // Get user details from request headers (set by CreateClientWithUser)
        var userId = Context.Request.Headers["Test-UserId"].FirstOrDefault();
        var userName = Context.Request.Headers["Test-UserName"].FirstOrDefault();
        var email = Context.Request.Headers["Test-Email"].FirstOrDefault();

        // Use header values if available, otherwise fall back to options or defaults
        var finalUserId = userId ?? Options.UserId ?? "test-user-id";
        var finalUserName = userName ?? Options.UserName ?? "test-user";
        var finalEmail = email ?? Options.Email ?? "test@example.com";

        // Create claims that match your JWT token structure
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, finalUserId),
            new Claim(ClaimTypes.Name, finalUserName),
            new Claim(ClaimTypes.Email, finalEmail),
            // Add additional claims as needed to match your JWT structure
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new Claim("jti", Guid.NewGuid().ToString())
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        // Log authentication for debugging
        Logger.LogInformation("Test authentication for user {UserId} ({UserName}) with email {Email}",
            finalUserId, finalUserName, finalEmail);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
