using System.Security.Claims;
using API.Dtos.Cards;
using API.Helpers;
using API.Logging;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Cards;

public static partial class CardsEndpoints
{
    private static void MapCardCommands(RouteGroupBuilder group)
    {
        group.MapPut("/{id:int}", UpdateCardFromIdAsync)
            .RequireAuthorization()
            .WithSummary("Update Card")
            .WithDescription("Updates card from form")
            .Produces<Card>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json");

        group.MapDelete("/{id:int}", DeleteCardFromIdAsync)
            .RequireAuthorization()
            .WithSummary("Delete card")
            .WithDescription("Removes card by ID from collection")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPost("/", AddNewCardAsync)
            .RequireAuthorization()
            .WithSummary("Add new card")
            .WithDescription("Adds card to collection with properties")
            .Produces<Card>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/card-list", AddCardListAsync)
            .RequireAuthorization()
            .WithSummary("Process card list")
            .WithDescription("Processes card names and returns Scryfall card data")
            .Produces<LinkedList<ScryfallCardDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> UpdateCardFromIdAsync(
        IUnitOfWork unit,
        int id,
        HttpContext context,
        [FromBody] UpdateCollectionCardDto updateDto,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger)
    {
        const string operation = "Cards.Update";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return ProblemResultFactory.Create(
                context,
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                "You must be logged in to update cards.",
                "card-update-auth-required");
        }

        logger.LogOperationStart(operation, new { id });

        var card = await unit.Repository<Card>().GetByIdAsync(id);
        if (card is null)
        {
            logger.LogOperationWarning(operation, "Card not found", new { id });
            return ProblemResultFactory.Create(
                context,
                StatusCodes.Status404NotFound,
                "Card not found",
                $"No card with id {id} exists in your collections.",
                "card-not-found");
        }

        var collection = await unit.Repository<Collection>().GetByIdAsync(card.CollectionId);
        if (collection is null)
        {
            logger.LogOperationWarning(operation, "Collection missing", new { card.CollectionId });
            return ProblemResultFactory.Create(
                context,
                StatusCodes.Status404NotFound,
                "Collection not found",
                "The collection associated with this card could not be located.",
                "collection-not-found");
        }

        if (collection.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Unauthorized", new { collection.Id, userId });
            return ProblemResultFactory.Create(
                context,
                StatusCodes.Status401Unauthorized,
                "Unauthorized collection access",
                "You do not have permission to modify cards in this collection.",
                "collection-access-denied");
        }
        if (updateDto.Quantity <= 0)
        {
            logger.LogOperationWarning(operation, "Invalid quantity", new { id, updateDto.Quantity });
            return ProblemResultFactory.Create(
                context,
                StatusCodes.Status400BadRequest,
                "Invalid quantity",
                "Quantity must be greater than zero for a card update.",
                "card-invalid-quantity");
        }

        try
        {
            if (!Enum.TryParse(updateDto.Condition, out Condition condition))
            {
                logger.LogOperationWarning(operation, "Invalid condition", new { id, updateDto.Condition });
                return ProblemResultFactory.Create(
                    context,
                    StatusCodes.Status400BadRequest,
                    "Invalid condition",
                    $"'{updateDto.Condition}' is not a supported condition value.",
                    "card-invalid-condition");
            }
            card.Collection = collection;
            card.Collection.NumberOfCards = card.Collection.NumberOfCards - card.Quantity + updateDto.Quantity;
            card.CollectionId = updateDto.CollectionId;
            card.Quantity = updateDto.Quantity;
            card.Language = updateDto.Language;
            card.Condition = condition;
            card.IsFoil = updateDto.IsFoil;
            card.PurchasePrice = updateDto.PurchasePrice;
            card.PurchasePriceCurrency = updateDto.PurchasePriceCurrency;
            card.IsMisprint = updateDto.IsMisprint;
            card.IsAltered = updateDto.IsAltered;

            unit.Repository<Card>().Update(card);
            unit.Repository<Collection>().Update(card.Collection);
            await unit.Complete();
        }
        catch (Exception ex)
        {
            logger.LogOperationFailure(operation, ex, new { id });
            return ProblemResultFactory.Create(
                context,
                StatusCodes.Status500InternalServerError,
                "Card update failed",
                "An unexpected error occurred while updating the card.",
                "card-update-error");
        }

        logger.LogOperationSuccess(operation, new { id });
        return Results.Ok(card);
    }

    private static async Task<IResult> DeleteCardFromIdAsync(
        IUnitOfWork unit,
        int id,
        CardDataService cds,
        HttpContext context,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger)
    {
        const string operation = "Cards.Delete";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id", new { id });
            return ProblemResultFactory.Create(
                context,
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                "You must be logged in to delete cards.",
                "card-delete-auth-required");
        }

        try
        {
            logger.LogOperationStart(operation, new { id });

            var card = await unit.Repository<Card>().GetByIdAsync(id);
            if (card is null)
            {
                logger.LogOperationWarning(operation, "Card not found", new { id });
                return ProblemResultFactory.Create(
                    context,
                    StatusCodes.Status404NotFound,
                    "Card not found",
                    $"No card with id {id} was found.",
                    "card-not-found");
            }

            var collection = await unit.Repository<Collection>().GetByIdAsync(card.CollectionId);
            if (collection is null)
            {
                logger.LogOperationWarning(operation, "Collection missing", new { card.CollectionId });
                return ProblemResultFactory.Create(
                    context,
                    StatusCodes.Status404NotFound,
                    "Collection not found",
                    "The collection associated with this card could not be located.",
                    "collection-not-found");
            }

            if (collection.OwnerId != userId)
            {
                logger.LogOperationWarning(operation, "Unauthorized", new { collection.Id, userId });
                return ProblemResultFactory.Create(
                    context,
                    StatusCodes.Status401Unauthorized,
                    "Unauthorized collection access",
                    "You do not have permission to modify cards in this collection.",
                    "collection-access-denied");
            }

            unit.Repository<Card>().Delete(card);
            collection.NumberOfCards = Math.Max(0, collection.NumberOfCards - card.Quantity);
            unit.Repository<Collection>().Update(collection);
            await unit.Complete();

            logger.LogOperationSuccess(operation, new { id });
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogOperationFailure(operation, ex, new { id });
            return ProblemResultFactory.Create(
                context,
                StatusCodes.Status500InternalServerError,
                "Card deletion failed",
                "An unexpected error occurred while removing the card.",
                "card-delete-error");
        }
    }

    private static async Task<IResult> AddNewCardAsync(
        IUnitOfWork unit,
        CardDataService cds,
        HttpContext context,
        InternalCardDto cardDto,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger)
    {
        const string operation = "Cards.AddInternal";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        var collection = await unit.Repository<Collection>().GetByIdAsync(cardDto.CollectionId);
        if (collection is null)
        {
            logger.LogOperationWarning(operation, "Collection not found", new { cardDto.CollectionId });
            return Results.NotFound();
        }
        if (collection.OwnerId != userId)
        {
            logger.LogOperationWarning(operation, "Unauthorized", new { cardDto.CollectionId, userId });
            return Results.Unauthorized();
        }

        if (userId != collection.OwnerId)
        {
            logger.LogOperationWarning(operation, "Collection mismatch", new { cardDto.CollectionId, userId });
            return Results.BadRequest("Collection is not valid for logged user");
        }

        if (!cds.CardDataById.TryGetValue(cardDto.ScryfallId, out var scryfallCardDto)) return Results.BadRequest("Card not found for scryfallId");

        if (!Enum.TryParse(cardDto.Condition, out Condition myEnum))
        {
            return Results.BadRequest("Invalid condition");
        }
        var imageUris = CardDataService.ResolveImageUris(scryfallCardDto);
        var imageUrl = imageUris.Normal ?? imageUris.Large ?? imageUris.Png ?? throw new InvalidOperationException($"Missing image URL for card {scryfallCardDto.Name}");
        var artCrop = imageUris.ArtCrop ?? throw new InvalidOperationException($"Missing art crop for card {scryfallCardDto.Name}");
        var backImageUrl = cds.ResolveBackImageUrl(scryfallCardDto);

        var card = new Card
        {
            ScryfallId = scryfallCardDto.Id,
            Name = scryfallCardDto.Name,
            Collection = collection,
            Quantity = cardDto.Quantity,
            Language = cardDto.Language,
            Condition = myEnum,
            IsFoil = cardDto.IsFoil,
            PurchasePrice = cardDto.PurchasePrice,
            ImageUrl = imageUrl,
            PurchasePriceCurrency = cardDto.PurchasePriceCurrency,
            SetCode = scryfallCardDto.Set,
            SetName = scryfallCardDto.SetName,
            CollectorNumber = scryfallCardDto.CollectorNumber!,
            Rarity = scryfallCardDto.Rarity!,
            IsMisprint = cardDto.IsMisprint,
            IsAltered = cardDto.IsAltered,
            ArtCrop = artCrop,
            BackImageUrl = backImageUrl
        };

        unit.Repository<Card>().Add(card);

        collection.NumberOfCards += card.Quantity;
        unit.Repository<Collection>().Update(collection);

        await unit.Complete();
        logger.LogOperationSuccess(operation, new { card.Id, card.CollectionId });
        return Results.Ok(card);
    }

    private static Task<IResult> AddCardListAsync(
        CardDataService cds,
        CardListDto cardListDto,
        HttpContext context,
        [FromServices] ILogger<CardsEndpointsLogCategory> logger)
    {
        const string operation = "Cards.AddList";
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Task.FromResult(Results.Unauthorized());
        }

        var scryfallCardList = new LinkedList<ScryfallCardDto>();

        var cardList = cardListDto.CardList.Trim().Split('\n').Select(p => p.Trim());
        foreach (var inputCard in cardList)
        {
            if (!cds.CardDataByName.TryGetValue(inputCard, out var card))
            {
                logger.LogOperationWarning(operation, "Card not found", new { inputCard });
                continue;
            }
            scryfallCardList.AddLast(card);
        }

        logger.LogOperationSuccess(operation, new { Count = scryfallCardList.Count });
        return Task.FromResult(Results.Ok(scryfallCardList));
    }

}
