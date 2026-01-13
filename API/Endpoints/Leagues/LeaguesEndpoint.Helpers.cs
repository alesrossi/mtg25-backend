using API.Logging;
using API.Services;
using Core.Models.Identity;
using Microsoft.AspNetCore.Http;
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

    private static async Task<IResult> MapLeagueServiceException(
        LeagueServiceException exception,
        HttpContext context,
        IMessageLocalizer messageLocalizer,
        string? userId)
    {
        return exception.StatusCode switch
        {
            StatusCodes.Status400BadRequest => exception.IncludeBody
                ? Results.BadRequest(await LocalizeBodyAsync(context, messageLocalizer, userId, exception.Body))
                : Results.BadRequest(),
            StatusCodes.Status401Unauthorized => Results.Unauthorized(),
            StatusCodes.Status404NotFound => exception.IncludeBody
                ? Results.NotFound(await LocalizeBodyAsync(context, messageLocalizer, userId, exception.Body))
                : Results.NotFound(),
            _ => Results.StatusCode(exception.StatusCode)
        };
    }

    private static async Task<object?> LocalizeBodyAsync(
        HttpContext context,
        IMessageLocalizer messageLocalizer,
        string? userId,
        object? body)
    {
        if (body is string messageKey)
        {
            return await messageLocalizer.GetMessageAsync(userId ?? string.Empty, messageKey, context.RequestAborted);
        }

        return body;
    }
}
