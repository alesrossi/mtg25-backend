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

namespace API.Endpoints.Accounts;

public static partial class AccountsEndpoints
{
    private static void MapAccountCommands(RouteGroupBuilder group)
    {
        group.MapPost("/register", RegisterUserAsync)
            .WithSummary("Register new user")
            .WithDescription("Creates a new user account with the provided registration details including name, email, and password")
            .Produces<UserDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPost("/login", LoginUserAsync)
            .WithSummary("Authenticate user login")
            .WithDescription("Authenticates a user with email and password credentials, returning user information upon successful login")
            .Produces<AuthDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/logout", LogoutUserAsync)
            .RequireAuthorization()
            .WithSummary("Logout user")
            .WithDescription("Logs out the authenticated user by blacklisting their JWT token")
            .Produces<string>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPut("/settings", UpdateSettingsAsync)
            .RequireAuthorization()
            .WithSummary("Update user settings")
            .WithDescription("Updates the authenticated user's settings")
            .Produces<SettingsForUserDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    }

    private static async Task<IResult> RegisterUserAsync(
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] IValidationService validationService,
        [FromBody] RegisterDto registerDto,
        [FromServices] ILogger<AccountsEndpointLogCategory> logger)
    {
        const string operation = "Accounts.Register";
        using var scope = logger.BeginOperationScope(operation, registerDto.Email);
        logger.LogOperationStart(operation, new { registerDto.Email });

        if (await CheckEmailExistsAsyncHelper(userManager, registerDto.Email))
        {
            logger.LogOperationWarning(operation, "Email exists", new { registerDto.Email });
            return Results.BadRequest("Email address already in use");
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
            return Results.BadRequest("Error in user creation");
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
        [FromBody] LoginDto loginDto,
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

        logger.LogOperationSuccess(operation, new { loginDto.Email, user.Id });
        return Results.Ok(new AuthDto
        {
            Token = token,
            ExpiryDate = DateTime.UtcNow.AddHours(1),
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = user.DisplayName
        });
    }

    private static async Task<IResult> LogoutUserAsync(
        HttpContext context,
        IJwtService jwtService,
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

        await jwtService.BlacklistTokenAsync(token, TimeSpan.FromHours(1));

        logger.LogOperationSuccess(operation);
        return Results.Ok(new { message = "Logged out successfully" });
    }

    private static async Task<IResult> UpdateSettingsAsync(
        HttpContext context,
        [FromBody] UpdateSettingsDto updateDto,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] AppIdentityDbContext dbContext,
        [FromServices] ILogger<AccountsEndpointLogCategory> logger)
    {
        const string operation = "Settings.Update";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        logger.LogOperationStart(operation, new { userId });

        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { userId });
            return ProblemResultFactory.Create(
                context,
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                "You must be logged in to update your settings.",
                "settings-update-auth-required");
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning(operation, "Missing user", new { userId });
            return ProblemResultFactory.Create(
                context,
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                "You must be logged in to update your settings.",
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
            return Results.BadRequest("Settings payload is required.");
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

        if (!string.IsNullOrWhiteSpace(updateDto.LanguageUi))
        {
            settings.LanguageUi = updateDto.LanguageUi;
        }

        if (!string.IsNullOrWhiteSpace(updateDto.LanguageCards))
        {
            settings.LanguageCards = updateDto.LanguageCards;
        }

        if (updateDto.EnabledLocation.HasValue)
        {
            settings.EnabledLocation = updateDto.EnabledLocation.Value;
        }

        await dbContext.SaveChangesAsync();

        logger.LogOperationSuccess(operation, new { userId, settings.Id });
        return Results.Ok(MapToDto(settings, user));
    }
}
