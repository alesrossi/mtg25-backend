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
    private readonly TimeProvider _timeProvider;

    public TestAuthenticationHandler(
        IOptionsMonitor<TestAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TimeProvider timeProvider)
        : base(options, logger, encoder, new TimeProviderSystemClock(timeProvider))
    {
        _timeProvider = timeProvider;
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

        // If no authentication headers are present, fail authentication (for unauthenticated tests)
        if (string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(Options.UserId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Use header values if available, otherwise fall back to options or defaults
        var finalUserId = userId ?? Options.UserId;
        var finalUserName = userName ?? Options.UserName;
        var finalEmail = email ?? Options.Email;

        // Create claims that match your JWT token structure
        var issuedAt = _timeProvider.GetUtcNow();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, finalUserId),
            new Claim(ClaimTypes.Name, finalUserName),
            new Claim(ClaimTypes.Email, finalEmail),
            // Add additional claims as needed to match your JWT structure
            new Claim("iat", issuedAt.ToUnixTimeSeconds().ToString()),
            new Claim("jti", Guid.NewGuid().ToString())
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// Temporary adapter until AuthenticationHandler gains a TimeProvider constructor.
    /// </summary>
    private sealed class TimeProviderSystemClock : ISystemClock
    {
        private readonly TimeProvider _provider;

        public TimeProviderSystemClock(TimeProvider provider) => _provider = provider;

        public DateTimeOffset UtcNow => _provider.GetUtcNow();
    }
}
