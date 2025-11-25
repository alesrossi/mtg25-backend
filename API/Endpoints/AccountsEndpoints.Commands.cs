using System;
using API.Dtos.Accounts;
using API.Services;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API.Endpoints;

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
        [FromBody] RegisterDto registerDto)
    {
        if (await CheckEmailExistsAsyncHelper(userManager, registerDto.Email))
        {
            return Results.BadRequest("Email address already in use");
        }

        var (isValid, errors) = validationService.ValidateModel(registerDto);
        if (!isValid)
        {
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

        if (!result.Succeeded) return Results.BadRequest("Error in user creation");

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
        [FromBody] LoginDto loginDto)
    {
        var user = await userManager.FindByEmailAsync(loginDto.Email);
        if (user == null) return Results.Unauthorized();

        var result = await signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

        if (!result.Succeeded) return Results.Unauthorized();

        var token = await jwtService.GenerateTokenAsync(user);

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
        IJwtService jwtService)
    {
        var token = context.Request.Headers.Authorization
            .ToString().Replace("Bearer ", "");

        if (string.IsNullOrEmpty(token))
            return Results.Unauthorized();

        await jwtService.BlacklistTokenAsync(token, TimeSpan.FromHours(1));

        return Results.Ok(new { message = "Logged out successfully" });
    }
}
