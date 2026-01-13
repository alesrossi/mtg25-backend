using System.Security.Claims;
using API.Dtos.Collections;
using API.Logging;
using API.Services;
using Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Collections;

public static partial class CollectionsEndpoints
{
    private static async Task<IResult> AddNewCollectionAsync(
        HttpContext context,
        NewCollectionDto collectionDto,
        [FromServices] ICollectionService collectionService,
        [FromServices] ILogger<CollectionsEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Collections.Create";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        try
        {
            var collection = await collectionService.CreateCollectionAsync(collectionDto, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { collection.Id, userId });
            return Results.Ok(collection);
        }
        catch (CollectionServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { userId });
            return await MapCollectionServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> UpdateCollectionAsync(
        int id,
        HttpContext context,
        NewCollectionDto collectionDto,
        [FromServices] ICollectionService collectionService,
        [FromServices] ILogger<CollectionsEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Collections.Update";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            var collection = await collectionService.UpdateCollectionAsync(id, collectionDto, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id });
            return Results.Ok(collection);
        }
        catch (CollectionServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return await MapCollectionServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> ImportCardList(IUnitOfWork unitOfWork,
        IFormFile file,
        int id,
        HttpContext context,
        [FromServices] ILogger<CollectionsEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        [FromServices] ICollectionService collectionService,
        CancellationToken cancellationToken,
        [FromQuery] ImportSource source = ImportSource.Manabox)
    {
        const string operation = "Collections.Import";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            var importResult = await collectionService.ImportCardsAsync(id, userId, file, source, cancellationToken);
            var importedCount = importResult.Cards.Sum(card => card.Quantity);
            logger.LogOperationSuccess(operation, new { id, importedCount, importResult.SkippedLines });
            return Results.Ok(new
            {
                cards = importResult.Cards,
                errors = importResult.Errors,
                skippedLines = importResult.SkippedLines
            });
        }
        catch (CollectionServiceException ex)
        {
            if (ex.StatusCode == StatusCodes.Status500InternalServerError)
            {
                logger.LogOperationFailure(operation, ex, new { id });
            }
            else
            {
                logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            }
            return await MapCollectionServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> DeleteCollectionAsync(
        int id,
        HttpContext context,
        [FromServices] ICollectionService collectionService,
        [FromServices] ILogger<CollectionsEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Collections.Delete";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            await collectionService.DeleteCollectionAsync(id, userId, cancellationToken);
            logger.LogOperationSuccess(operation, new { id });
            return Results.NoContent();
        }
        catch (CollectionServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return await MapCollectionServiceException(ex, context, messageLocalizer, userId);
        }
    }

    private static async Task<IResult> MassDeleteCardsFromCollection(
        int id,
        [FromBody] List<int>? ctbd,
        HttpContext context,
        [FromServices] ICollectionService collectionService,
        [FromServices] ILogger<CollectionsEndpointLogCategory> logger,
        [FromServices] IMessageLocalizer messageLocalizer,
        CancellationToken cancellationToken)
    {
        const string operation = "Collections.MassDelete";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        try
        {
            var removedCount = await collectionService.MassDeleteCardsAsync(id, userId, ctbd, cancellationToken);
            logger.LogOperationSuccess(operation, new { id, Removed = removedCount });
            return Results.Ok(removedCount);
        }
        catch (CollectionServiceException ex)
        {
            logger.LogOperationWarning(operation, ex.Message, new { id, userId });
            return await MapCollectionServiceException(ex, context, messageLocalizer, userId);
        }
    }
}
