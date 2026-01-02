using System.Globalization;
using System.Security.Claims;
using API.Dtos.Cards;
using API.Helpers;
using API.Logging;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Microsoft.AspNetCore.Mvc;
using static API.Helpers.CollectionValueCalculator;

namespace API.Endpoints.Cards;

public static partial class CardsEndpoints
{
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
            var previousValue = CalculateCardValue(card.PurchasePrice, card.Quantity);

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

            var updatedValue = CalculateCardValue(card.PurchasePrice, card.Quantity);
            card.Collection.TotalPrice = ApplyTotalPriceDelta(card.Collection.TotalPrice, updatedValue - previousValue);

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
    
    private static async Task<IResult> UpdateCardVersionFromIdAsync(
        IUnitOfWork unit,
        int id,
        HttpContext context,
        CardDataService cds,
        [FromBody] UpdateCollectionCardWithSFIdDto updateDto,
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

            if (!cds.CardDataById.TryGetValue(updateDto.ScryfallId, out var scryfallCardDto))
            {
                logger.LogOperationWarning(operation, "Invalid scryfallId", new { id, updateDto.ScryfallId });
                return ProblemResultFactory.Create(
                    context,
                    StatusCodes.Status400BadRequest,
                    "Invalid scryfallId",
                    $"'{updateDto.ScryfallId}' is not a valid id.",
                    "card-invalid-sf-id");
            }

            if (scryfallCardDto.Name != card.Name)
            {
                logger.LogOperationWarning(operation, "Invalid version", new { id, updateDto.ScryfallId });
                return ProblemResultFactory.Create(
                    context,
                    StatusCodes.Status400BadRequest,
                    "Invalid version",
                    $"'{updateDto.ScryfallId}' is not a valid version for card '{card.Name}'.",
                    "card-invalid-version");
            }
            
            var previousValue = CalculateCardValue(card.PurchasePrice, card.Quantity);

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
            card.ScryfallId =  updateDto.ScryfallId;
            card.ArtCrop = scryfallCardDto.ImageUris!.ArtCrop!;
            card.ImageUrl = scryfallCardDto.ImageUris!.Large!;
            card.SetCode = scryfallCardDto.SetId!;
            card.SetName = scryfallCardDto.SetName!;
            card.CollectorNumber = scryfallCardDto.CollectorNumber!;
            card.Rarity = scryfallCardDto.Rarity!;

            var updatedValue = CalculateCardValue(card.PurchasePrice, card.Quantity);
            card.Collection.TotalPrice = ApplyTotalPriceDelta(card.Collection.TotalPrice, updatedValue - previousValue);
            
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

            var removedValue = CalculateCardValue(card.PurchasePrice, card.Quantity);
            unit.Repository<Card>().Delete(card);
            collection.NumberOfCards = Math.Max(0, collection.NumberOfCards - card.Quantity);
            collection.TotalPrice = ApplyTotalPriceDelta(collection.TotalPrice, -removedValue);
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
        [FromServices] IUserSettingsService userSettingsService,
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

        var (resolvedPrice, resolvedCurrency) = await ResolvePurchasePriceAsync(
            cardDto,
            scryfallCardDto,
            userId,
            userSettingsService);

        var card = new Card
        {
            ScryfallId = scryfallCardDto.Id,
            Name = scryfallCardDto.Name,
            Collection = collection,
            Quantity = cardDto.Quantity,
            Language = cardDto.Language,
            Condition = myEnum,
            IsFoil = cardDto.IsFoil,
            PurchasePrice = resolvedPrice,
            ImageUrl = imageUrl,
            PurchasePriceCurrency = resolvedCurrency,
            SetCode = scryfallCardDto.Set,
            SetName = scryfallCardDto.SetName,
            TypeLine = scryfallCardDto.TypeLine ?? string.Empty,
            CollectorNumber = scryfallCardDto.CollectorNumber!,
            Rarity = scryfallCardDto.Rarity!,
            IsMisprint = cardDto.IsMisprint,
            IsAltered = cardDto.IsAltered,
            ArtCrop = artCrop,
            BackImageUrl = backImageUrl
        };

        unit.Repository<Card>().Add(card);

        collection.NumberOfCards += card.Quantity;
        var addedValue = CalculateCardValue(card.PurchasePrice, card.Quantity);
        collection.TotalPrice = ApplyTotalPriceDelta(collection.TotalPrice, addedValue);
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

    private static async Task<(double price, string currency)> ResolvePurchasePriceAsync(
        InternalCardDto cardDto,
        ScryfallCardDto scryfallCard,
        string userId,
        IUserSettingsService userSettingsService)
    {
        var purchasePrice = cardDto.PurchasePrice;
        var purchaseCurrency = cardDto.PurchasePriceCurrency;

        var requiresMarketPrice = !purchasePrice.HasValue || purchasePrice.Value <= 0;
        var requiresCurrency = string.IsNullOrWhiteSpace(purchaseCurrency);

        if (!requiresMarketPrice && !requiresCurrency)
        {
            return (purchasePrice!.Value, purchaseCurrency!);
        }

        var marketProvider = await userSettingsService.GetMarketProviderAsync(userId);
        var currencyCode = ConvertCurrencyToCode(userSettingsService.ResolveCurrency(marketProvider));

        if (requiresMarketPrice)
        {
            var livePrice = ResolveMarketPrice(scryfallCard, cardDto.IsFoil, marketProvider);
            if (livePrice.HasValue)
            {
                purchasePrice = livePrice.Value;
                purchaseCurrency ??= currencyCode;
            }
        }

        purchaseCurrency ??= currencyCode;

        return (purchasePrice ?? 0, purchaseCurrency);
    }

    private static double? ResolveMarketPrice(ScryfallCardDto scryfallCard, bool isFoil, MarketProvider marketProvider)
    {
        var prices = scryfallCard.Prices;
        if (prices is null)
        {
            return null;
        }

        var priceText = marketProvider == MarketProvider.Mkm
            ? (isFoil ? prices.EurFoil : prices.Eur)
            : (isFoil ? prices.UsdFoil : prices.Usd);

        if (string.IsNullOrWhiteSpace(priceText) && isFoil)
        {
            priceText = marketProvider == MarketProvider.Mkm
                ? prices.Eur
                : prices.Usd;
        }

        if (string.IsNullOrWhiteSpace(priceText))
        {
            return null;
        }

        return double.TryParse(priceText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string ConvertCurrencyToCode(Currency currency)
    {
        return currency.ToString().ToUpperInvariant();
    }
}
