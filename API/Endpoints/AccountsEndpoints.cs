using API.Dtos;
using Core.Models.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints;

public static class AccountsEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/accounts").WithTags("Accounts");
        group.MapGet("/emailexists/{email}", CheckEmailExistsAsync)
            .WithSummary("Check if email exists")
            .WithDescription("Verifies if an email address is already registered in the system");
        group.MapPost("/register", RegisterUserAsync)
            .WithSummary("Register new user")
            .WithDescription("Creates a new user account with the provided registration details including name, email, and password");
        group.MapPost("/login", LoginUserAsync)
            .WithSummary("Authenticate user login")
            .WithDescription("Authenticates a user with email and password credentials, returning user information upon successful login");
    }
    
    
    

    private static async Task<bool> CheckEmailExistsAsyncHelper(UserManager<AppUser> userManager, string email)
    {
        return await userManager.FindByEmailAsync(email) != null;
    }
    
    private static async Task<IResult> CheckEmailExistsAsync(string email, [FromServices] UserManager<AppUser> userManager) 
    {
        var emailExists = await CheckEmailExistsAsyncHelper(userManager, email);
        return Results.Ok(emailExists);
    }
    
    private static async Task<IResult> RegisterUserAsync([FromServices] UserManager<AppUser> userManager, [FromBody] RegisterDto registerDto)
    {
        if (await CheckEmailExistsAsyncHelper(userManager, registerDto.Email))
        {
            return Results.BadRequest("Email address is in use");
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
    
    private static async Task<IResult> LoginUserAsync([FromServices] UserManager<AppUser> userManager, [FromServices] SignInManager<AppUser> signInManager, [FromBody] LoginDto loginDto)
    {
            
        var user = await userManager.FindByEmailAsync(loginDto.Email);

        if (user == null) return Results.Unauthorized();

        var result = await signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

        if (!result.Succeeded) return Results.Unauthorized();

        return Results.Ok(new UserDto
        {
            DisplayName = user.DisplayName,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName
        });
    }
}