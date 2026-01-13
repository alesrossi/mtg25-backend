using API.Logging;
using API.Services;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;

namespace API.Endpoints.Leagues;

public static partial class LeaguesEndpoint
{
    private static async Task<bool> EnsureRoleAsync(
        UserManager<AppUser> userManager,
        AppUser user,
        string role,
        ILogger<LeaguesEndpointLogCategory> logger,
        string operation)
    {
        if (await userManager.IsInRoleAsync(user, role))
        {
            return true;
        }

        var result = await userManager.AddToRoleAsync(user, role);
        if (result.Succeeded)
        {
            return true;
        }

        logger.LogOperationWarning(operation, "Role assignment failed", new
        {
            userId = user.Id,
            role,
            Errors = result.Errors.Select(e => new { e.Code, e.Description })
        });

        return false;
    }

    private static IResult MapLeagueServiceException(LeagueServiceException exception)
    {
        return exception.StatusCode switch
        {
            StatusCodes.Status400BadRequest => exception.IncludeBody ? Results.BadRequest(exception.Body) : Results.BadRequest(),
            StatusCodes.Status401Unauthorized => Results.Unauthorized(),
            StatusCodes.Status404NotFound => exception.IncludeBody ? Results.NotFound(exception.Body) : Results.NotFound(),
            _ => Results.StatusCode(exception.StatusCode)
        };
    }
}
