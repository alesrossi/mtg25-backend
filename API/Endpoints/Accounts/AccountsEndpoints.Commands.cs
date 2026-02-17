using System.Security.Claims;
using API.Dtos.Accounts;
using API.Helpers;
using API.Logging;
using API.Services;
using Core.Models.Identity;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API.Endpoints.Accounts;

public static partial class AccountsEndpoints
{
    private const string RefreshTokenCookieName = "mtg25.refresh";

    private static async Task<IResult> RegisterUserAsync(
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] IValidationService validationService,
        [FromBody] RegisterDto registerDto,
        HttpContext context,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<AccountsEndpointLogCategory> logger)
    {
        const string operation = "Accounts.Register";
        using var scope = logger.BeginOperationScope(operation, registerDto.Email);
        logger.LogOperationStart(operation, new { registerDto.Email });

        if (await CheckEmailExistsAsyncHelper(userManager, registerDto.Email))
        {
            logger.LogOperationWarning(operation, "Email exists", new { registerDto.Email });
            return await LocalizedErrorResultFactory.BadRequestMessageAsync(
                context,
                messageLocalizer,
                null,
                "Errors.Accounts.EmailInUse");
        }

        var (isValid, errors) = validationService.ValidateModel(registerDto);
        if (!isValid)
        {
            logger.LogOperationWarning(operation, "Validation failed", new { registerDto.Email, errors });
            return Results.BadRequest(new { errors });
        }
        
        
        var user = new AppUser
        {
            DisplayName = registerDto.DisplayName,
            Email = registerDto.Email,
            UserName = registerDto.Email,
            FirstName = registerDto.FirstName,
            LastName = registerDto.LastName,
            Settings = null
        };

        user.Settings = new Settings
        {
            AppUser = user,
            AppUserId = user.Id
        };

        var result = await userManager.CreateAsync(user, registerDto.Password);

        if (!result.Succeeded)
        {
            logger.LogOperationWarning(operation, "Identity creation failed", result.Errors);
            return await LocalizedErrorResultFactory.BadRequestMessageAsync(
                context,
                messageLocalizer,
                null,
                "Errors.Accounts.UserCreationFailed");
        }

        logger.LogOperationSuccess(operation, new { registerDto.Email });
        return Results.Ok(new UserDto
        {
            DisplayName = user.DisplayName,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName
        });
    }

    private static async Task<IResult> LoginUserAsync(
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] IJwtService jwtService,
        [FromServices] SignInManager<AppUser> signInManager,
        [FromServices] IOptions<JwtSettings> jwtOptions,
        [FromBody] LoginDto loginDto,
        HttpContext context,
        [FromServices] ILogger<AccountsEndpointLogCategory> logger)
    {
        const string operation = "Accounts.Login";
        using var scope = logger.BeginOperationScope(operation, loginDto.Email);
        logger.LogOperationStart(operation, new { loginDto.Email });

        var user = await userManager.FindByEmailAsync(loginDto.Email);
        if (user == null)
        {
            logger.LogOperationWarning(operation, "User not found", new { loginDto.Email });
            return Results.Unauthorized();
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

        if (!result.Succeeded)
        {
            logger.LogOperationWarning(operation, "Password sign-in failed", new { loginDto.Email });
            return Results.Unauthorized();
        }

        var token = await jwtService.GenerateTokenAsync(user);
        var refreshToken = await jwtService.GenerateRefreshTokenAsync(user.Id);
        SetRefreshTokenCookie(context, refreshToken, jwtOptions.Value.RefreshTokenExpiryDays);

        logger.LogOperationSuccess(operation, new { loginDto.Email, user.Id });
        return Results.Ok(new AuthDto
        {
            Token = token,
            ExpiryDate = DateTime.UtcNow.AddMinutes(jwtOptions.Value.ExpiryMinutes),
            RefreshToken = loginDto.IncludeRefreshToken ? refreshToken : null,
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = user.DisplayName
        });
    }

    private static async Task<IResult> LogoutUserAsync(
        HttpContext context,
        IJwtService jwtService,
        [FromServices] IOptions<JwtSettings> jwtOptions,
        [FromServices] ILogger<AccountsEndpointLogCategory> logger)
    {
        const string operation = "Accounts.Logout";
        logger.LogOperationStart(operation);

        var token = context.Request.Headers.Authorization
            .ToString().Replace("Bearer ", "");

        if (string.IsNullOrEmpty(token))
        {
            logger.LogOperationWarning(operation, "Missing token");
            return Results.Unauthorized();
        }

        await jwtService.BlacklistTokenAsync(token, TimeSpan.FromMinutes(jwtOptions.Value.ExpiryMinutes));

        var refreshTokenHeader = context.Request.Headers["X-Refresh-Token"].ToString();
        var refreshToken = string.IsNullOrWhiteSpace(refreshTokenHeader)
            ? (context.Request.Cookies.TryGetValue(RefreshTokenCookieName, out var cookieToken) ? cookieToken : null)
            : refreshTokenHeader;

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            await jwtService.RevokeRefreshTokenAsync(refreshToken, userId);
            ClearRefreshTokenCookie(context);
        }

        logger.LogOperationSuccess(operation);
        return Results.Ok(new { message = "Logged out successfully" });
    }

    private static async Task<IResult> RefreshTokenAsync(
        HttpContext context,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] IJwtService jwtService,
        [FromServices] IOptions<JwtSettings> jwtOptions,
        [FromServices] ILogger<AccountsEndpointLogCategory> logger,
        [FromBody] RefreshTokenRequestDto? refreshRequest)
    {
        const string operation = "Accounts.Refresh";
        logger.LogOperationStart(operation);

        var refreshTokenHeader = context.Request.Headers["X-Refresh-Token"].ToString();
        var refreshToken = !string.IsNullOrWhiteSpace(refreshTokenHeader)
            ? refreshTokenHeader
            : refreshRequest?.RefreshToken;

        if (string.IsNullOrWhiteSpace(refreshToken) &&
            context.Request.Cookies.TryGetValue(RefreshTokenCookieName, out var cookieToken))
        {
            refreshToken = cookieToken;
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            logger.LogOperationWarning(operation, "Missing refresh token");
            return Results.Unauthorized();
        }

        var userId = await jwtService.ValidateRefreshTokenAsync(refreshToken);
        if (string.IsNullOrWhiteSpace(userId))
        {
            logger.LogOperationWarning(operation, "Invalid refresh token");
            ClearRefreshTokenCookie(context);
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning(operation, "User not found", new { userId });
            await jwtService.RevokeRefreshTokenAsync(refreshToken, userId);
            ClearRefreshTokenCookie(context);
            return Results.Unauthorized();
        }

        await jwtService.RevokeRefreshTokenAsync(refreshToken, userId);
        var newRefreshToken = await jwtService.GenerateRefreshTokenAsync(userId);
        SetRefreshTokenCookie(context, newRefreshToken, jwtOptions.Value.RefreshTokenExpiryDays);

        var token = await jwtService.GenerateTokenAsync(user);

        logger.LogOperationSuccess(operation, new { user.Id });
        return Results.Ok(new AuthDto
        {
            Token = token,
            ExpiryDate = DateTime.UtcNow.AddMinutes(jwtOptions.Value.ExpiryMinutes),
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = user.DisplayName
        });
    }

    private static async Task<IResult> UpdateSettingsAsync(
        HttpContext context,
        [FromBody] UpdateSettingsDto updateDto,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] AppIdentityDbContext dbContext,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<AccountsEndpointLogCategory> logger)
    {
        const string operation = "Settings.Update";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        logger.LogOperationStart(operation, new { userId });

        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { userId });
            return await LocalizedErrorResultFactory.ProblemAsync(
                context,
                messageLocalizer,
                userId,
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                "Errors.Accounts.SettingsUpdateAuthRequired",
                "settings-update-auth-required");
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning(operation, "Missing user", new { userId });
            return await LocalizedErrorResultFactory.ProblemAsync(
                context,
                messageLocalizer,
                userId,
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                "Errors.Accounts.SettingsUpdateAuthRequired",
                "settings-update-auth-required");
        }

        var settings = await dbContext.Settings
            .FirstOrDefaultAsync(s => s.AppUserId == userId);

        if (settings is null)
        {
            logger.LogOperationWarning(operation, "Settings not found", new { userId });
            return Results.NotFound();
        }

        if (updateDto is null)
        {
            logger.LogOperationWarning(operation, "Missing payload", new { userId });
            return await LocalizedErrorResultFactory.BadRequestMessageAsync(
                context,
                messageLocalizer,
                userId,
                "Errors.Accounts.SettingsRequired");
        }

        if (updateDto.MarketProvider.HasValue)
        {
            settings.MarketProvider = updateDto.MarketProvider.Value;
        }

        if (updateDto.ReferencePrice.HasValue)
        {
            settings.ReferencePrice = updateDto.ReferencePrice.Value;
        }

        if (updateDto.Currency.HasValue)
        {
            settings.Currency = updateDto.Currency.Value;
        }

        if (updateDto.LanguageUi.HasValue)
        {
            settings.LanguageUi = updateDto.LanguageUi.Value;
        }

        if (updateDto.LanguageCards.HasValue)
        {
            settings.LanguageCards = updateDto.LanguageCards.Value;
        }

        if (updateDto.EnabledLocation.HasValue)
        {
            settings.EnabledLocation = updateDto.EnabledLocation.Value;
        }

        if (updateDto.CompanionName is not null)
        {
            user.CompanionName = updateDto.CompanionName;
            await userManager.UpdateAsync(user);
        }

        await dbContext.SaveChangesAsync();

        logger.LogOperationSuccess(operation, new { userId, settings.Id });
        return Results.Ok(MapToDto(settings, user));
    }

    private static void SetRefreshTokenCookie(HttpContext context, string refreshToken, int expiryDays)
    {
        var isHttps = context.Request.IsHttps;
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(expiryDays),
            Path = "/"
        };

        context.Response.Cookies.Append(RefreshTokenCookieName, refreshToken, options);
    }

    private static void ClearRefreshTokenCookie(HttpContext context)
    {
        var isHttps = context.Request.IsHttps;
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = isHttps,
            SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/"
        };

        context.Response.Cookies.Delete(RefreshTokenCookieName, options);
    }
}
