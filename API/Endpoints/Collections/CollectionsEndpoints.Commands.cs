using System;
using System.Security.Claims;
using API.Dtos.Collections;
using API.Helpers;
using API.Logging;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Core.Specifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using static API.Helpers.CollectionValueCalculator;

namespace API.Endpoints.Collections;

public static partial class CollectionsEndpoints
{
    private static async Task<IResult> AddNewCollectionAsync(
        [FromServices] IUnitOfWork unitOfWork,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] IValidationService validationService,
        HttpContext context,
        NewCollectionDto collectionDto,
        [FromServices] ILogger<CollectionsEndpointLogCategory> logger)
    {
        const string operation = "Collections.Create";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            logger.LogOperationWarning(operation, "User not found", new { userId });
            return Results.Unauthorized();
        }

        var (isValid, errors) = validationService.ValidateModel(collectionDto);
        if (!isValid)
        {
            logger.LogOperationWarning(operation, "Validation failed", new { userId, errors });
            return Results.BadRequest(new { errors });
        }

        var collection = new Collection
        {
            Name = collectionDto.Name,
            Color = collectionDto.Color,
            NumberOfCards = 0,
            TotalPrice = 0,
            OwnerId = user.Id
        };
        unitOfWork.Repository<Collection>().Add(collection);
        await unitOfWork.Complete();
        logger.LogOperationSuccess(operation, new { collection.Id, userId });
        return Results.Ok(collection);
    }

    private static async Task<IResult> UpdateCollectionAsync(
        int id,
        [FromServices] IUnitOfWork unitOfWork,
        [FromServices] UserManager<AppUser> userManager,
        [FromServices] IValidationService validationService,
        HttpContext context,
        NewCollectionDto collectionDto,
        [FromServices] ILogger<CollectionsEndpointLogCategory> logger)
    {
        const string operation = "Collections.Update";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var (isValid, errors) = validationService.ValidateModel(collectionDto);
        if (!isValid)
        {
            logger.LogOperationWarning(operation, "Validation failed", new { id, errors });
            return Results.BadRequest(new { errors });
        }

        var collection = await unitOfWork.Repository<Collection>().GetByIdAsync(id);
        if (collection is null)
        {
            logger.LogOperationWarning(operation, "Collection not found", new { id });
            return Results.NotFound();
        }
        if (collection.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Unauthorized access", new { id, userId });
            return Results.Unauthorized();
        }

        collection.Name = collectionDto.Name;
        collection.Color = collectionDto.Color;
        unitOfWork.Repository<Collection>().Update(collection);
        await unitOfWork.Complete();

        logger.LogOperationSuccess(operation, new { id });
        return Results.Ok(collection);
    }

    private static async Task<IResult> ImportCardList(IUnitOfWork unitOfWork,
        CardDataService cds,
        IFormFile file,
        int id,
        HttpContext context,
        [FromServices] IUserSettingsService userSettingsService,
        [FromServices] ILogger<CollectionsEndpointLogCategory> logger,
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
            if (file.Length <= 0)
            {
                logger.LogOperationWarning(operation, "Empty file", new { id });
                return Results.BadRequest("No file uploaded");
            }
            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogOperationWarning(operation, "Invalid file type", new { id, file.FileName });
                return Results.BadRequest("File must be csv");
            }
            if (file.Length > 10 * 1024 * 1024)
            {
                logger.LogOperationWarning(operation, "File too large", new { id, file.Length });
                return Results.BadRequest("File is too large");
            }

            var marketProvider = await userSettingsService.GetMarketProviderAsync(userId);
            var userCurrency = userSettingsService.ResolveCurrency(marketProvider);
            var importResult = source switch
            {
                ImportSource.Manabox => await CollectionHelpers.ProcessCsvFIle(file, cds, id, marketProvider, userCurrency),
                ImportSource.Moxfield => await CollectionHelpers.ProcessMoxfieldCsvFile(file, cds, id, marketProvider, userCurrency),
                _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unsupported import source.")
            };

            var collection = await unitOfWork.Repository<Collection>().GetByIdAsync(id);
            if (collection is null)
            {
                logger.LogOperationWarning(operation, "Collection not found", new { id });
                return Results.NotFound();
            }
            if (collection.OwnerId != userId)
            {
                logger.LogOperationWarning(operation, "Unauthorized", new { id, userId });
                return Results.Unauthorized();
            }

            if (importResult.Cards.Count == 0)
            {
                var errors = importResult.Errors.Any()
                    ? importResult.Errors
                    : ["CSV file did not contain any valid cards."];

                logger.LogOperationWarning(operation, "No valid cards", new { id, errors, importResult.SkippedLines });
                return Results.BadRequest(new
                {
                    cards = importResult.Cards,
                    errors,
                    skippedLines = importResult.SkippedLines
                });
            }

            var importedCount = importResult.Cards.Sum(card => card.Quantity);
            var existingCards = await unitOfWork.Repository<Card>()
                .ListAsync(new CardsForCollectionSpecification(id), tracking: false) ?? [];
            var existingTotal = existingCards.Sum(card => card.PurchasePrice * Math.Max(0, card.Quantity));
            var importedTotal = importResult.Cards.Sum(card => card.PurchasePrice * Math.Max(0, card.Quantity));

            collection.NumberOfCards += importedCount;
            collection.TotalPrice = Math.Round(existingTotal + importedTotal, 2, MidpointRounding.AwayFromZero);

            unitOfWork.Repository<Card>().Add(importResult.Cards);
            unitOfWork.Repository<Collection>().Update(collection);
            await unitOfWork.Complete();

            logger.LogOperationSuccess(operation, new { id, importedCount, importResult.SkippedLines });
            return Results.Ok(new
            {
                cards = importResult.Cards,
                errors = importResult.Errors,
                skippedLines = importResult.SkippedLines
            });
        }
        catch (Exception ex)
        {
            logger.LogOperationFailure(operation, ex, new { id });
            return Results.StatusCode(500);
        }
    }

    private static async Task<IResult> DeleteCollectionAsync(
        int id,
        IUnitOfWork unitOfWork,
        HttpContext context,
        [FromServices] ILogger<CollectionsEndpointLogCategory> logger)
    {
        const string operation = "Collections.Delete";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var collection = await unitOfWork.Repository<Collection>().GetByIdAsync(id);
        if (collection is null)
        {
            logger.LogOperationWarning(operation, "Collection not found", new { id });
            return Results.NotFound();
        }
        if (collection.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Unauthorized", new { id, userId });
            return Results.Unauthorized();
        }

        unitOfWork.Repository<Collection>().Delete(collection);
        await unitOfWork.Complete();

        logger.LogOperationSuccess(operation, new { id });
        return Results.NoContent();
    }

    private static async Task<IResult> MassDeleteCardsFromCollection(
        IUnitOfWork unitOfWork,
        int id,
        [FromBody] List<int>? ctbd,
        HttpContext context,
        [FromServices] ILogger<CollectionsEndpointLogCategory> logger)
    {
        const string operation = "Collections.MassDelete";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return Results.Unauthorized();
        }

        var collection = await unitOfWork.Repository<Collection>().GetByIdAsync(id);
        if (collection is null)
        {
            logger.LogOperationWarning(operation, "Collection not found", new { id });
            return Results.NotFound();
        }
        if (collection.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Unauthorized", new { id, userId });
            return Results.Unauthorized();
        }

        if (ctbd is null || ctbd.Count == 0)
        {
            logger.LogOperationWarning(operation, "No ids", new { id });
            return Results.BadRequest("No card ids provided.");
        }

        var cardsToDelete = await unitOfWork.Repository<Card>().ListAsync(new CardsByIdsSpecification(ctbd, id));
        if (cardsToDelete is null || cardsToDelete.Count == 0)
        {
            logger.LogOperationWarning(operation, "Cards not found", new { id, ctbd.Count });
            return Results.NotFound();
        }

        var totalRemovedQuantity = 0;
        var totalRemovedValue = 0d;
        foreach (var card in cardsToDelete)
        {
            unitOfWork.Repository<Card>().Delete(card);
            totalRemovedQuantity += Math.Max(0, card.Quantity);
            totalRemovedValue += CalculateCardValue(card.PurchasePrice, card.Quantity);
        }

        collection.NumberOfCards = Math.Max(0, collection.NumberOfCards - totalRemovedQuantity);
        collection.TotalPrice = ApplyTotalPriceDelta(collection.TotalPrice, -totalRemovedValue);
        unitOfWork.Repository<Collection>().Update(collection);

        await unitOfWork.Complete();

        logger.LogOperationSuccess(operation, new { id, Removed = cardsToDelete.Count });
        return Results.Ok(cardsToDelete.Count);
    }
}
