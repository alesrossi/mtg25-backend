using System.Security.Claims;
using API.Dtos.Cards;
using API.Helpers;
using API.Logging;
using API.Services;
using Core.Specifications;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Collections;

public static partial class CollectionsEndpoints
{
    private static async Task<IResult> GetCollectionFromIdAsync(
        int id,
        HttpContext context,
        [FromServices] ICollectionService collectionService,
        [FromServices] ILogger<CollectionsEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Collections.GetById";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            var collection = await collectionService.GetCollectionByIdAsync(id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id });
            return Results.Ok(collection);
        }
        catch (CollectionServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return MapCollectionServiceException(ex);
        }
    }

    private static async Task<IResult> GetCardsFromCollectionAsync(
        int id,
        [AsParameters] EntitySpecParams entityParams,
        HttpContext context,
        [FromServices] ICollectionService collectionService,
        [FromServices] ILogger<CollectionsEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Collections.GetCards";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            var result = await collectionService.GetCardsFromCollectionAsync(id, userId, entityParams, cancellationToken);
            if (!string.IsNullOrEmpty(entityParams.GroupBy))
            {
                logger.LogOperationSuccess(operation, new { id, entityParams.GroupBy, Grouped = true });
            }
            else if (result is Pagination<ExtensiveCardDto> pagination)
            {
                logger.LogOperationSuccess(operation, new { id, entityParams.PageIndex, entityParams.PageSize, entityParams.Sort, Count = pagination.Data.Count });
            }
            else
            {
                logger.LogOperationSuccess(operation, new { id });
            }
            return Results.Ok(result);
        }
        catch (CollectionServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return MapCollectionServiceException(ex);
        }
    }

    private static async Task<IResult> GetAllCollectionsForUser(
        HttpContext context,
        [FromServices] ICollectionService collectionService,
        [FromServices] ILogger<CollectionsEndpointLogCategory> logger,
        CancellationToken cancellationToken)
    {
        const string operation = "Collections.List";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        try
        {
            var collections = await collectionService.GetCollectionsForUserAsync(userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { userId, Count = collections.Count });
            return Results.Ok(collections);
        }
        catch (CollectionServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { userId });
            return MapCollectionServiceException(ex);
        }
    }
}
