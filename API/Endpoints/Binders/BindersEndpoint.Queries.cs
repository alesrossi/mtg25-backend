using System.Security.Claims;
using API.Dtos.Binders;
using API.Logging;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Binders;

public static partial class BindersEndpoint
{
    private static async Task<IResult> GetBindersAsync(
        HttpContext context,
        [FromServices] IBindersService bindersService,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.List";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        try
        {
            var dto = await bindersService.GetBindersAsync(userId);
            logger.LogOperationSuccess(operation, new { userId, Count = dto.Count });
            return Results.Ok(dto);
        }
        catch (BindersServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { userId });
            return MapBindersServiceException(ex);
        }
    }

    private static async Task<IResult> GetBinderByIdAsync(
        int id,
        HttpContext context,
        [FromServices] IBindersService bindersService,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Get";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        try
        {
            var dto = await bindersService.GetBinderByIdAsync(id, userId ?? string.Empty);
            logger.LogOperationSuccess(operation, new { id, Cards = dto.CardsCount });
            return Results.Ok(dto);
        }
        catch (BindersServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return MapBindersServiceException(ex);
        }
    }

    private static async Task<IResult> GetBinderCardsAsync(
        int binderId,
        HttpContext context,
        [FromServices] IBindersService bindersService,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Cards.List";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        try
        {
            var dto = await bindersService.GetBinderCardsAsync(binderId, userId ?? string.Empty);
            logger.LogOperationSuccess(operation, new { binderId, Count = dto.Count });
            return Results.Ok(dto);
        }
        catch (BindersServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { binderId, userId });
            return MapBindersServiceException(ex);
        }
    }

    private static async Task<IResult> GetBinderCardByIdAsync(
        int binderId,
        int binderCardId,
        HttpContext context,
        [FromServices] IBindersService bindersService,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Cards.Get";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        try
        {
            var dto = await bindersService.GetBinderCardByIdAsync(binderId, binderCardId, userId ?? string.Empty);
            logger.LogOperationSuccess(operation, new { binderId, binderCardId });
            return Results.Ok(dto);
        }
        catch (BindersServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { binderId, binderCardId, userId });
            return MapBindersServiceException(ex);
        }
    }
}
