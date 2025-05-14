using API.Dtos;
using Core.Models.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapGet("/accounts/emailexists/{email}", async (string email, [FromServices] UserManager<AppUser> userManager) =>
        {
            var emailExists = await CheckEmailExistsAsyncHelper(userManager, email);
            return Results.Ok(emailExists);

        });
    
        app.MapPost("/accounts/register", async ([FromServices] UserManager<AppUser> userManager, [FromBody] RegisterDto registerDto) =>
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
        });
        
        app.MapPost("/accounts/login", async ([FromServices] UserManager<AppUser> userManager, [FromServices] SignInManager<AppUser> signInManager, [FromBody] LoginDto loginDto) =>
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
        });
    }
    
    
    

    private static async Task<bool> CheckEmailExistsAsyncHelper(UserManager<AppUser> userManager, string email)
    {
        return await userManager.FindByEmailAsync(email) != null;
    }
    
   
    
    
}