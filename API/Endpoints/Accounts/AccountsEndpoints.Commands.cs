using API.Dtos.Accounts;
using API.Logging;
using API.Services;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Accounts;

public static partial class AccountsEndpoints
{
    private static void MapAccountCommands(RouteGroupBuilder group)
    {
        group.MapPost("/register", RegisterUserAsync)
            .WithSummary("Register new user")
            .WithDescription("Creates a new user account with the provided registration details including name, email, and password")
            .Produces<UserDto>()
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/login", LoginUserAsync)
            .WithSummary("Authenticate user login")
            .WithDescription("Authenticates a user with email and password credentials, returning user information upon successful login")
            .Produces<AuthDto>()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/logout", LogoutUserAsync)
            .RequireAuthorization()
            .WithSummary("Logout user")
            .WithDescription("Logs out the authenticated user by blacklisting their JWT token")
            .Produces<string>()
            .Produces(StatusCodes.Status401Unauthorized);
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
}
