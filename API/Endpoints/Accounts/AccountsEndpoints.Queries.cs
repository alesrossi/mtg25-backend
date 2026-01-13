using System.Security.Claims;
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
    
    private static async Task<IResult> GetSettingsAsync(
        HttpContext context,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] AppIdentityDbContext dbContext,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ILogger<AccountsEndpointLogCategory> logger)
    {
        const string operation = "Settings.Get";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { userId });
            return await LocalizedErrorResultFactory.ProblemAsync(
                context,
                messageLocalizer,
                userId,
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                "Errors.Accounts.SettingsGetAuthRequired",
                "settings-get-auth-required");
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
                "Errors.Accounts.SettingsGetAuthRequired",
                "settings-get-auth-required");
        }

        var res = await dbContext.Settings
            .Where(ul => ul.AppUserId == userId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (res is null)
        {
            logger.LogOperationWarning(operation, "Settings not found", new { userId });
            return Results.NotFound();
        }
        
        return Results.Ok(MapToDto(res, user));
    }
}
