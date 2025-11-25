using API.Dtos.Accounts;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API.Endpoints;

public static partial class AccountsEndpoints
{
    private static void MapAccountQueries(RouteGroupBuilder group)
    {
        group.MapGet("/emailexists/{email}", CheckEmailExistsAsync)
            .WithSummary("Check if email exists")
            .WithDescription("Verifies if an email address is already registered in the system")
            .Produces<bool>();
    }

    private static async Task<IResult> CheckEmailExistsAsync(
        string email,
        [FromServices] UserManager<AppUser> userManager)
    {
        return Results.Ok(await CheckEmailExistsAsyncHelper(userManager, email));
    }
}
