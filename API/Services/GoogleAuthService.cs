using API.Configuration;
using API.Logging;
using Core.Models.Identity;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace API.Services;

public interface IGoogleTokenValidator
{
    Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken, string clientId);
}

public class GoogleTokenValidator : IGoogleTokenValidator
{
    public Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken, string clientId)
    {
        var settings = new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] };
        return GoogleJsonWebSignature.ValidateAsync(idToken, settings);
    }
}

public interface IGoogleAuthService
{
    Task<GoogleAuthResult> AuthenticateAsync(string idToken);
}

public class GoogleAuthService : IGoogleAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IGoogleTokenValidator _tokenValidator;
    private readonly GoogleAuthConfig _config;
    private readonly ILogger<GoogleAuthService> _logger;
    private const string Operation = "Accounts.GoogleAuth";

    public GoogleAuthService(
        UserManager<AppUser> userManager,
        IGoogleTokenValidator tokenValidator,
        IOptions<GoogleAuthConfig> config,
        ILogger<GoogleAuthService> logger)
    {
        _userManager = userManager;
        _tokenValidator = tokenValidator;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<GoogleAuthResult> AuthenticateAsync(string idToken)
    {
        using var scope = _logger.BeginOperationScope(Operation);

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await _tokenValidator.ValidateAsync(idToken, _config.ClientId);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogOperationWarning(Operation, "Invalid Google ID token", new { ex.Message });
            return GoogleAuthResult.Fail("Invalid Google token.");
        }

        var email = payload.Email;
        var firstName = payload.GivenName ?? string.Empty;
        var lastName = payload.FamilyName ?? string.Empty;

        _logger.LogOperationStep(Operation, "Token validated", new { email });

        var user = await _userManager.FindByEmailAsync(email);

        if (user is not null)
        {
            if (!user.IsGoogleAccount)
            {
                _logger.LogOperationWarning(Operation, "Login failed — email belongs to password account", new { email });
                return GoogleAuthResult.Fail("An account with this email already exists. Please sign in with your password.");
            }

            _logger.LogOperationSuccess(Operation, new { action = "login", email, user.Id });
            return GoogleAuthResult.Ok(user);
        }

        // Auto-register new Google user
        _logger.LogOperationStep(Operation, "New Google user — starting registration", new { email, firstName, lastName });

        user = new AppUser
        {
            Email = email,
            UserName = email,
            FirstName = firstName,
            LastName = lastName,
            DisplayName = firstName.Length > 0 ? firstName : email.Split('@')[0],
            IsGoogleAccount = true,
            Settings = null
        };

        user.Settings = new Settings
        {
            AppUser = user,
            AppUserId = user.Id
        };

        var result = await _userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogOperationWarning(Operation, "Registration failed — user creation error", new { email, errors });
            return GoogleAuthResult.Fail("Failed to create account.");
        }

        _logger.LogOperationSuccess(Operation, new { action = "registration", email, user.Id });
        return GoogleAuthResult.Ok(user);
    }
}

public class GoogleAuthResult
{
    public bool Succeeded { get; private init; }
    public AppUser? User { get; private init; }
    public string? Error { get; private init; }

    public static GoogleAuthResult Ok(AppUser user) => new() { Succeeded = true, User = user };
    public static GoogleAuthResult Fail(string error) => new() { Succeeded = false, Error = error };
}
