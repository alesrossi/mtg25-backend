using System.Security.Claims;
using API.Dtos.Binders;
using API.Logging;
using API.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Binders;

public static partial class BindersEndpoint
{
    private static async Task<IResult> CreateBinderAsync(
        CreateBinderDto createDto,
        HttpContext context,
        [FromServices] IBindersService bindersService,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Create";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        logger.LogOperationStart(operation, new { createDto.Name });

        try
        {
            var binder = await bindersService.CreateBinderAsync(createDto, userId);
            logger.LogOperationSuccess(operation, new { binder.Id });
            return Results.Ok(binder);
        }
        catch (BindersServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { userId });
            return MapBindersServiceException(ex);
        }
    }

    private static async Task<IResult> UpdateBinderAsync(
        int id,
        UpdateBinderDto updateDto,
        HttpContext context,
        [FromServices] IBindersService bindersService,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Update";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            var binder = await bindersService.UpdateBinderAsync(id, updateDto, userId);
            logger.LogOperationSuccess(operation, new { id });
            return Results.Ok(binder);
        }
        catch (BindersServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return MapBindersServiceException(ex);
        }
    }

    private static async Task<IResult> DeleteBinderAsync(
        int id,
        HttpContext context,
        [FromServices] IBindersService bindersService,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Delete";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            await bindersService.DeleteBinderAsync(id, userId);
            logger.LogOperationSuccess(operation, new { id });
            return Results.NoContent();
        }
        catch (BindersServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return MapBindersServiceException(ex);
        }
    }

    private static async Task<IResult> CreateBinderCardAsync(
        int binderId,
        List<CreateBinderCardDto> createDtoList,
        HttpContext context,
        [FromServices] IBindersService bindersService,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Cards.Create";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        try
        {
            var cardList = await bindersService.CreateBinderCardsAsync(binderId, createDtoList, userId ?? string.Empty);
            logger.LogOperationSuccess(operation, new { binderId, Added = cardList.Count });
            return Results.Ok(cardList);
        }
        catch (BindersServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { binderId, userId });
            return MapBindersServiceException(ex);
        }
    }

    private static async Task<IResult> UpdateBinderCardAsync(
        int binderId,
        int binderCardId,
        UpdateBinderCardDto updateDto,
        HttpContext context,
        [FromServices] IBindersService bindersService,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Cards.Update";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        try
        {
            var updated = await bindersService.UpdateBinderCardAsync(binderId, binderCardId, updateDto, userId ?? string.Empty);
            logger.LogOperationSuccess(operation, new { binderId, binderCardId });
            return Results.Ok(updated);
        }
        catch (BindersServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { binderId, binderCardId, userId });
            return MapBindersServiceException(ex);
        }
    }

    private static async Task<IResult> DeleteBinderCardAsync(
        int binderId,
        int binderCardId,
        HttpContext context,
        [FromServices] IBindersService bindersService,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Cards.Delete";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        try
        {
            await bindersService.DeleteBinderCardAsync(binderId, binderCardId, userId ?? string.Empty);
            logger.LogOperationSuccess(operation, new { binderId, binderCardId });
            return Results.NoContent();
        }
        catch (BindersServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { binderId, binderCardId, userId });
            return MapBindersServiceException(ex);
        }
    }
}
