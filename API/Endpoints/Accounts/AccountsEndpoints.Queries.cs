using API.Logging;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Accounts;

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
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] ILogger<AccountsEndpointLogCategory> logger)
    {
        const string operation = "Accounts.EmailExists";
        logger.LogOperationStart(operation, new { email });

        var exists = await CheckEmailExistsAsyncHelper(userManager, email);
        logger.LogOperationSuccess(operation, new { email, exists });
        return Results.Ok(exists);
    }
}
