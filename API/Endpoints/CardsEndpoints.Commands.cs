using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using API.Dtos.Cards;
using API.Helpers;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace API.Endpoints;

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
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .WithOpenApi(operation =>
            {
                SetProblemExample(operation, StatusCodes.Status400BadRequest, new OpenApiObject
                {
                    ["type"] = new OpenApiString("https://httpstatuses.io/400"),
                    ["title"] = new OpenApiString("Invalid quantity"),
                    ["status"] = new OpenApiInteger(400),
                    ["detail"] = new OpenApiString("Quantity must be greater than zero for a card update."),
                    ["instance"] = new OpenApiString("/api/cards/42"),
                    ["traceId"] = new OpenApiString("00-11111111111111111111111111111111-aaaaaaaaaaaaaaaa-00"),
                    ["errorCode"] = new OpenApiString("card-invalid-quantity")
                });

                SetProblemExample(operation, StatusCodes.Status401Unauthorized, new OpenApiObject
                {
                    ["type"] = new OpenApiString("https://httpstatuses.io/401"),
                    ["title"] = new OpenApiString("Authentication required"),
                    ["status"] = new OpenApiInteger(401),
                    ["detail"] = new OpenApiString("You must be logged in to update cards."),
                    ["instance"] = new OpenApiString("/api/cards/42"),
                    ["traceId"] = new OpenApiString("00-22222222222222222222222222222222-bbbbbbbbbbbbbbbb-00"),
                    ["errorCode"] = new OpenApiString("card-update-auth-required")
                });

                SetProblemExample(operation, StatusCodes.Status404NotFound, new OpenApiObject
                {
                    ["type"] = new OpenApiString("https://httpstatuses.io/404"),
                    ["title"] = new OpenApiString("Card not found"),
                    ["status"] = new OpenApiInteger(404),
                    ["detail"] = new OpenApiString("No card with id 42 exists in your collections."),
                    ["instance"] = new OpenApiString("/api/cards/42"),
                    ["traceId"] = new OpenApiString("00-33333333333333333333333333333333-cccccccccccccccc-00"),
                    ["errorCode"] = new OpenApiString("card-not-found")
                });

                SetProblemExample(operation, StatusCodes.Status500InternalServerError, new OpenApiObject
                {
                    ["type"] = new OpenApiString("https://httpstatuses.io/500"),
                    ["title"] = new OpenApiString("Card update failed"),
                    ["status"] = new OpenApiInteger(500),
                    ["detail"] = new OpenApiString("An unexpected error occurred while updating the card."),
                    ["instance"] = new OpenApiString("/api/cards/42"),
                    ["traceId"] = new OpenApiString("00-44444444444444444444444444444444-dddddddddddddddd-00"),
                    ["errorCode"] = new OpenApiString("card-update-error")
                });

                return operation;
            });

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
            .WithDescription("Processes card names and returns Oracle card data")
            .Produces<LinkedList<OracleCardDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> UpdateCardFromIdAsync(
        IUnitOfWork unit,
        int id,
        HttpContext context,
        [FromBody] UpdateCollectionCardDto updateDto)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            return ProblemResultFactory.Create(
                context,
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                "You must be logged in to update cards.",
                "card-update-auth-required");
        }

        var card = await unit.Repository<Card>().GetByIdAsync(id);
        if (card is null)
        {
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
            return ProblemResultFactory.Create(
                context,
                StatusCodes.Status404NotFound,
                "Collection not found",
                "The collection associated with this card could not be located.",
                "collection-not-found");
        }

        if (collection.OwnerId != userId)
        {
            return ProblemResultFactory.Create(
                context,
                StatusCodes.Status401Unauthorized,
                "Unauthorized collection access",
                "You do not have permission to modify cards in this collection.",
                "collection-access-denied");
        }
        if (updateDto.Quantity <= 0)
        {
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
        catch (Exception)
        {
            return ProblemResultFactory.Create(
                context,
                StatusCodes.Status500InternalServerError,
                "Card update failed",
                "An unexpected error occurred while updating the card.",
                "card-update-error");
        }

        return Results.Ok(card);
    }

    private static async Task<IResult> DeleteCardFromIdAsync(
        IUnitOfWork unit,
        int id,
        CardDataService cds,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
        {
            return ProblemResultFactory.Create(
                context,
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                "You must be logged in to delete cards.",
                "card-delete-auth-required");
        }

        try
        {
            var card = await unit.Repository<Card>().GetByIdAsync(id);
            if (card is null)
            {
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
                return ProblemResultFactory.Create(
                    context,
                    StatusCodes.Status404NotFound,
                    "Collection not found",
                    "The collection associated with this card could not be located.",
                    "collection-not-found");
            }

            if (collection.OwnerId != userId)
            {
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

            return Results.NoContent();
        }
        catch (Exception)
        {
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
        InternalCardDto cardDto)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();

        var collection = await unit.Repository<Collection>().GetByIdAsync(cardDto.CollectionId);
        if (collection is null) return Results.NotFound();
        if (collection.OwnerId != userId) return Results.Unauthorized();

        if (userId != collection.OwnerId) return Results.BadRequest("Collection is not valid for logged user");

        if (!cds.CardDataById.TryGetValue(cardDto.OracleId, out var oracleCard)) return Results.BadRequest("Card not found for oracleId");

        if (!Enum.TryParse(cardDto.Condition, out Condition myEnum))
        {
            return Results.BadRequest("Invalid condition");
        }
        var imageUris = CardDataService.ResolveImageUris(oracleCard);
        var imageUrl = imageUris.Normal ?? imageUris.Large ?? imageUris.Png ?? throw new InvalidOperationException($"Missing image URL for card {oracleCard.Name}");
        var artCrop = imageUris.ArtCrop ?? throw new InvalidOperationException($"Missing art crop for card {oracleCard.Name}");
        var backImageUrl = cds.ResolveBackImageUrl(oracleCard);

        var card = new Card
        {
            OracleId = oracleCard.Id,
            Name = oracleCard.Name,
            Collection = collection,
            Quantity = cardDto.Quantity,
            Language = cardDto.Language,
            Condition = myEnum,
            IsFoil = cardDto.IsFoil,
            PurchasePrice = cardDto.PurchasePrice,
            ImageUrl = imageUrl,
            PurchasePriceCurrency = cardDto.PurchasePriceCurrency,
            SetCode = oracleCard.Set,
            SetName = oracleCard.SetName,
            CollectorNumber = oracleCard.CollectorNumber!,
            Rarity = oracleCard.Rarity!,
            IsMisprint = cardDto.IsMisprint,
            IsAltered = cardDto.IsAltered,
            ArtCrop = artCrop,
            BackImageUrl = backImageUrl
        };

        unit.Repository<Card>().Add(card);

        collection.NumberOfCards += card.Quantity;
        unit.Repository<Collection>().Update(collection);

        await unit.Complete();
        return Results.Ok(card);
    }

    private static Task<IResult> AddCardListAsync(
        CardDataService cds,
        CardListDto cardListDto,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Task.FromResult(Results.Unauthorized());

        var oracleCardList = new LinkedList<OracleCardDto>();

        var cardList = cardListDto.CardList.Trim().Split('\n').Select(p => p.Trim());
        foreach (var inputCard in cardList)
        {
            oracleCardList.AddLast(cds.CardDataByName[inputCard]);
        }

        return Task.FromResult(Results.Ok(oracleCardList));
    }

    private static void SetProblemExample(OpenApiOperation operation, int statusCode, IOpenApiAny example)
    {
        var statusKey = statusCode.ToString();
        if (!operation.Responses.TryGetValue(statusKey, out var response))
        {
            return;
        }

        if (!response.Content.TryGetValue("application/problem+json", out var mediaType))
        {
            return;
        }

        mediaType.Example = example;
    }
}
