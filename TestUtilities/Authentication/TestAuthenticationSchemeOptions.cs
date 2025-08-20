using Microsoft.AspNetCore.Authentication;

namespace TestUtilities.Authentication;

/// <summary>
/// Configuration options for test authentication.
/// Allows tests to specify which user context to simulate.
/// </summary>
public class TestAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// The user ID to simulate in test scenarios.
    /// Corresponds to the authenticated user's ID in JWT claims.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The username to simulate.
    /// Used for display purposes and username-based operations.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// The email to simulate.
    /// Used for email-based operations and user lookup.
    /// </summary>
    public string Email { get; set; } = string.Empty;
}