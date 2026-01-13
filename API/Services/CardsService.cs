using System.Globalization;
using API.Dtos.Cards;
using API.Helpers;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;

namespace API.Services;

public interface ICardsService
{
    Task<ExtensiveCardDto> GetCardByIdAsync(int id, string userId, CancellationToken cancellationToken = default);
    Task<List<MinimalCardDto>> SearchCardsAsync(string find, string userId, CancellationToken cancellationToken = default);
    Task<List<KeyValuePair<string, ScryfallCardDto>>> GetCardVersionsAsync(string name, string userId, CancellationToken cancellationToken = default);
    Task<ScryfallCardDto> GetCardFromExactNameAsync(string name, string userId, CancellationToken cancellationToken = default);
    Task<ScryfallCardDto> GetCardFromScryfallIdAsync(string id, string userId, CancellationToken cancellationToken = default);
    Task<Card> UpdateCardAsync(int id, UpdateCollectionCardDto updateDto, string userId, CancellationToken cancellationToken = default);
    Task<Card> UpdateCardVersionAsync(int id, UpdateCollectionCardWithSFIdDto updateDto, string userId, CancellationToken cancellationToken = default);
    Task DeleteCardAsync(int id, string userId, CancellationToken cancellationToken = default);
    Task<Card> AddNewCardAsync(InternalCardDto cardDto, string userId, CancellationToken cancellationToken = default);
    Task<LinkedList<ScryfallCardDto>> AddCardListAsync(CardListDto cardListDto, string userId, CancellationToken cancellationToken = default);
}

public sealed class CardsService : ICardsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly CardDataService _cardDataService;
    private readonly IUserSettingsService _userSettingsService;

    public CardsService(
        IUnitOfWork unitOfWork,
        CardDataService cardDataService,
        IUserSettingsService userSettingsService)
    {
        _unitOfWork = unitOfWork;
        _cardDataService = cardDataService;
        _userSettingsService = userSettingsService;
    }

    public async Task<ExtensiveCardDto> GetCardByIdAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw CardsServiceException.Unauthorized("Missing user id");
        }

        var card = await _unitOfWork.Repository<Card>().GetByIdAsync(id, tracking: false);
        if (card is null)
        {
            throw CardsServiceException.NotFound("Card not found");
        }

        var collection = await _unitOfWork.Repository<Collection>().GetByIdAsync(card.CollectionId, tracking: false);
        if (collection!.OwnerId != userId)
        {
            throw CardsServiceException.Unauthorized("Unauthorized");
        }

        var marketProvider = await _userSettingsService.GetMarketProviderAsync(userId);

        double? price = null;
        if (_cardDataService.CardDataById.TryGetValue(card.ScryfallId, out var marketData) && marketData?.Prices is not null)
        {
            var priceText = marketProvider == MarketProvider.Mkm
                ? (card.IsFoil ? marketData.Prices.EurFoil : marketData.Prices.Eur)
                : (card.IsFoil ? marketData.Prices.UsdFoil : marketData.Prices.Usd);

            if (!string.IsNullOrWhiteSpace(priceText) &&
                double.TryParse(priceText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                price = parsed;
            }
        }

        return MapToDto(card, price, marketProvider);
    }

    public Task<List<MinimalCardDto>> SearchCardsAsync(string find, string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw CardsServiceException.Unauthorized("Missing user id");
        }

        var cardList = _cardDataService.CardDataById
            .Where(x => x.Value.Name.Contains(find, StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.Value.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var result = cardList.Select(card =>
        {
            var imageUris = CardDataService.ResolveImageUris(card.Value);
            var imageUrl = imageUris?.Normal ?? imageUris?.Large ?? imageUris?.Png;
            var backImageUrl = _cardDataService.ResolveBackImageUrl(card.Value);

            return new MinimalCardDto
            {
                Name = card.Value.Name,
                ScryfallId = card.Key,
                ImageUrl = imageUrl,
                BackImageUrl = backImageUrl
            };
        }).ToList();

        if (result.Count == 0)
        {
            throw CardsServiceException.NotFound("Card not found", includeBody: true, body: "Card not found");
        }

        return Task.FromResult(result);
    }

    public Task<List<KeyValuePair<string, ScryfallCardDto>>> GetCardVersionsAsync(string name, string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw CardsServiceException.Unauthorized("Missing user id");
        }

        if (!_cardDataService.CardDataByName.ContainsKey(name))
        {
            throw CardsServiceException.NotFound("Card not found");
        }

        var versions = _cardDataService.CardDataById
            .Where(x => x.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult(versions);
    }

    public Task<ScryfallCardDto> GetCardFromExactNameAsync(string name, string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw CardsServiceException.Unauthorized("Missing user id");
        }

        if (_cardDataService.CardDataByName.TryGetValue(name, out var card))
        {
            return Task.FromResult(card);
        }

        throw CardsServiceException.NotFound("Card not found");
    }

    public Task<ScryfallCardDto> GetCardFromScryfallIdAsync(string id, string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw CardsServiceException.Unauthorized("Missing user id");
        }

        if (_cardDataService.CardDataById.TryGetValue(id, out var card))
        {
            return Task.FromResult(card);
        }

        throw CardsServiceException.NotFound("Card not found");
    }

    public async Task<Card> UpdateCardAsync(int id, UpdateCollectionCardDto updateDto, string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw CardsServiceException.Problem(
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                "You must be logged in to update cards.",
                "card-update-auth-required");
        }

        var card = await _unitOfWork.Repository<Card>().GetByIdAsync(id);
        if (card is null)
        {
            throw CardsServiceException.Problem(
                StatusCodes.Status404NotFound,
                "Card not found",
                $"No card with id {id} exists in your collections.",
                "card-not-found");
        }

        var collection = await _unitOfWork.Repository<Collection>().GetByIdAsync(card.CollectionId);
        if (collection is null)
        {
            throw CardsServiceException.Problem(
                StatusCodes.Status404NotFound,
                "Collection not found",
                "The collection associated with this card could not be located.",
                "collection-not-found");
        }

        if (collection.OwnerId != userId)
        {
            throw CardsServiceException.Problem(
                StatusCodes.Status401Unauthorized,
                "Unauthorized collection access",
                "You do not have permission to modify cards in this collection.",
                "collection-access-denied");
        }

        if (updateDto.Quantity <= 0)
        {
            throw CardsServiceException.Problem(
                StatusCodes.Status400BadRequest,
                "Invalid quantity",
                "Quantity must be greater than zero for a card update.",
                "card-invalid-quantity");
        }

        try
        {
            if (!Enum.TryParse(updateDto.Condition, out Condition condition))
            {
                throw CardsServiceException.Problem(
                    StatusCodes.Status400BadRequest,
                    "Invalid condition",
                    $"'{updateDto.Condition}' is not a supported condition value.",
                    "card-invalid-condition");
            }

            var previousValue = CollectionValueCalculator.CalculateCardValue(card.PurchasePrice, card.Quantity);

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

            var updatedValue = CollectionValueCalculator.CalculateCardValue(card.PurchasePrice, card.Quantity);
            card.Collection.TotalPrice = CollectionValueCalculator.ApplyTotalPriceDelta(card.Collection.TotalPrice, updatedValue - previousValue);

            _unitOfWork.Repository<Card>().Update(card);
            _unitOfWork.Repository<Collection>().Update(card.Collection);
            await _unitOfWork.Complete();
        }
        catch (CardsServiceException)
        {
            throw;
        }
        catch (Exception)
        {
            throw CardsServiceException.Problem(
                StatusCodes.Status500InternalServerError,
                "Card update failed",
                "An unexpected error occurred while updating the card.",
                "card-update-error");
        }

        return card;
    }

    public async Task<Card> UpdateCardVersionAsync(int id, UpdateCollectionCardWithSFIdDto updateDto, string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw CardsServiceException.Problem(
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                "You must be logged in to update cards.",
                "card-update-auth-required");
        }

        var card = await _unitOfWork.Repository<Card>().GetByIdAsync(id);
        if (card is null)
        {
            throw CardsServiceException.Problem(
                StatusCodes.Status404NotFound,
                "Card not found",
                $"No card with id {id} exists in your collections.",
                "card-not-found");
        }

        var collection = await _unitOfWork.Repository<Collection>().GetByIdAsync(card.CollectionId);
        if (collection is null)
        {
            throw CardsServiceException.Problem(
                StatusCodes.Status404NotFound,
                "Collection not found",
                "The collection associated with this card could not be located.",
                "collection-not-found");
        }

        if (collection.OwnerId != userId)
        {
            throw CardsServiceException.Problem(
                StatusCodes.Status401Unauthorized,
                "Unauthorized collection access",
                "You do not have permission to modify cards in this collection.",
                "collection-access-denied");
        }

        if (updateDto.Quantity <= 0)
        {
            throw CardsServiceException.Problem(
                StatusCodes.Status400BadRequest,
                "Invalid quantity",
                "Quantity must be greater than zero for a card update.",
                "card-invalid-quantity");
        }

        try
        {
            if (!Enum.TryParse(updateDto.Condition, out Condition condition))
            {
                throw CardsServiceException.Problem(
                    StatusCodes.Status400BadRequest,
                    "Invalid condition",
                    $"'{updateDto.Condition}' is not a supported condition value.",
                    "card-invalid-condition");
            }

            if (!_cardDataService.CardDataById.TryGetValue(updateDto.ScryfallId, out var scryfallCardDto))
            {
                throw CardsServiceException.Problem(
                    StatusCodes.Status400BadRequest,
                    "Invalid scryfallId",
                    $"'{updateDto.ScryfallId}' is not a valid id.",
                    "card-invalid-sf-id");
            }

            if (scryfallCardDto.Name != card.Name)
            {
                throw CardsServiceException.Problem(
                    StatusCodes.Status400BadRequest,
                    "Invalid version",
                    $"'{updateDto.ScryfallId}' is not a valid version for card '{card.Name}'.",
                    "card-invalid-version");
            }

            var previousValue = CollectionValueCalculator.CalculateCardValue(card.PurchasePrice, card.Quantity);

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
            card.ScryfallId = updateDto.ScryfallId;
            card.ArtCrop = scryfallCardDto.ImageUris!.ArtCrop!;
            card.ImageUrl = scryfallCardDto.ImageUris!.Large!;
            card.SetCode = scryfallCardDto.SetId!;
            card.SetName = scryfallCardDto.SetName!;
            card.CollectorNumber = scryfallCardDto.CollectorNumber!;
            card.Rarity = scryfallCardDto.Rarity!;

            var updatedValue = CollectionValueCalculator.CalculateCardValue(card.PurchasePrice, card.Quantity);
            card.Collection.TotalPrice = CollectionValueCalculator.ApplyTotalPriceDelta(card.Collection.TotalPrice, updatedValue - previousValue);

            _unitOfWork.Repository<Card>().Update(card);
            _unitOfWork.Repository<Collection>().Update(card.Collection);
            await _unitOfWork.Complete();
        }
        catch (CardsServiceException)
        {
            throw;
        }
        catch (Exception)
        {
            throw CardsServiceException.Problem(
                StatusCodes.Status500InternalServerError,
                "Card update failed",
                "An unexpected error occurred while updating the card.",
                "card-update-error");
        }

        return card;
    }

    public async Task DeleteCardAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw CardsServiceException.Problem(
                StatusCodes.Status401Unauthorized,
                "Authentication required",
                "You must be logged in to delete cards.",
                "card-delete-auth-required");
        }

        try
        {
            var card = await _unitOfWork.Repository<Card>().GetByIdAsync(id);
            if (card is null)
            {
                throw CardsServiceException.Problem(
                    StatusCodes.Status404NotFound,
                    "Card not found",
                    $"No card with id {id} was found.",
                    "card-not-found");
            }

            var collection = await _unitOfWork.Repository<Collection>().GetByIdAsync(card.CollectionId);
            if (collection is null)
            {
                throw CardsServiceException.Problem(
                    StatusCodes.Status404NotFound,
                    "Collection not found",
                    "The collection associated with this card could not be located.",
                    "collection-not-found");
            }

            if (collection.OwnerId != userId)
            {
                throw CardsServiceException.Problem(
                    StatusCodes.Status401Unauthorized,
                    "Unauthorized collection access",
                    "You do not have permission to modify cards in this collection.",
                    "collection-access-denied");
            }

            var removedValue = CollectionValueCalculator.CalculateCardValue(card.PurchasePrice, card.Quantity);
            _unitOfWork.Repository<Card>().Delete(card);
            collection.NumberOfCards = Math.Max(0, collection.NumberOfCards - card.Quantity);
            collection.TotalPrice = CollectionValueCalculator.ApplyTotalPriceDelta(collection.TotalPrice, -removedValue);
            _unitOfWork.Repository<Collection>().Update(collection);
            await _unitOfWork.Complete();
        }
        catch (CardsServiceException)
        {
            throw;
        }
        catch (Exception)
        {
            throw CardsServiceException.Problem(
                StatusCodes.Status500InternalServerError,
                "Card deletion failed",
                "An unexpected error occurred while removing the card.",
                "card-delete-error");
        }
    }

    public async Task<Card> AddNewCardAsync(InternalCardDto cardDto, string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw CardsServiceException.Unauthorized("Missing user id");
        }

        var collection = await _unitOfWork.Repository<Collection>().GetByIdAsync(cardDto.CollectionId);
        if (collection is null)
        {
            throw CardsServiceException.NotFound("Collection not found");
        }
        if (collection.OwnerId != userId)
        {
            throw CardsServiceException.Unauthorized("Unauthorized");
        }

        if (userId != collection.OwnerId)
        {
            throw CardsServiceException.BadRequest("Collection is not valid for logged user", includeBody: true);
        }

        if (!_cardDataService.CardDataById.TryGetValue(cardDto.ScryfallId, out var scryfallCardDto))
        {
            throw CardsServiceException.BadRequest("Card not found for scryfallId", includeBody: true);
        }

        if (!Enum.TryParse(cardDto.Condition, out Condition myEnum))
        {
            throw CardsServiceException.BadRequest("Invalid condition", includeBody: true);
        }

        var imageUris = CardDataService.ResolveImageUris(scryfallCardDto);
        var imageUrl = imageUris.Normal ?? imageUris.Large ?? imageUris.Png ?? throw new InvalidOperationException($"Missing image URL for card {scryfallCardDto.Name}");
        var artCrop = imageUris.ArtCrop ?? throw new InvalidOperationException($"Missing art crop for card {scryfallCardDto.Name}");
        var backImageUrl = _cardDataService.ResolveBackImageUrl(scryfallCardDto);

        var (resolvedPrice, resolvedCurrency) = await ResolvePurchasePriceAsync(
            cardDto,
            scryfallCardDto,
            userId,
            _userSettingsService);

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

        _unitOfWork.Repository<Card>().Add(card);

        collection.NumberOfCards += card.Quantity;
        var addedValue = CollectionValueCalculator.CalculateCardValue(card.PurchasePrice, card.Quantity);
        collection.TotalPrice = CollectionValueCalculator.ApplyTotalPriceDelta(collection.TotalPrice, addedValue);
        _unitOfWork.Repository<Collection>().Update(collection);

        await _unitOfWork.Complete();
        return card;
    }

    public Task<LinkedList<ScryfallCardDto>> AddCardListAsync(CardListDto cardListDto, string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw CardsServiceException.Unauthorized("Missing user id");
        }

        var scryfallCardList = new LinkedList<ScryfallCardDto>();

        var cardList = cardListDto.CardList.Trim().Split('\n').Select(p => p.Trim());
        foreach (var inputCard in cardList)
        {
            if (!_cardDataService.CardDataByName.TryGetValue(inputCard, out var card))
            {
                continue;
            }
            scryfallCardList.AddLast(card);
        }

        return Task.FromResult(scryfallCardList);
    }

    private static ExtensiveCardDto MapToDto(Card card, double? price, MarketProvider? priceCurrency)
    {
        return new ExtensiveCardDto
        {
            Id = card.Id,
            Name = card.Name,
            ScryfallId = card.ScryfallId,
            CollectionId = card.CollectionId,
            Quantity = card.Quantity,
            Language = card.Language,
            Condition = card.Condition,
            IsFoil = card.IsFoil,
            PurchasePrice = card.PurchasePrice,
            PurchasePriceCurrency = card.PurchasePriceCurrency,
            ImageUrl = card.ImageUrl,
            BackImageUrl = card.BackImageUrl,
            ArtCrop = card.ArtCrop,
            SetCode = card.SetCode,
            SetName = card.SetName,
            TypeLine = card.TypeLine,
            CollectorNumber = card.CollectorNumber,
            Rarity = card.Rarity,
            IsMisprint = card.IsMisprint,
            IsAltered = card.IsAltered,
            Price = price,
            PriceCurrency = priceCurrency
        };
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

public sealed class CardsServiceException : Exception
{
    public int StatusCode { get; }
    public object? Body { get; }
    public bool IncludeBody { get; }
    public string? Title { get; }
    public string? Detail { get; }
    public string? ErrorCode { get; }

    private CardsServiceException(int statusCode, string? message, object? body, bool includeBody, string? title, string? detail, string? errorCode)
        : base(message ?? string.Empty)
    {
        StatusCode = statusCode;
        Body = body;
        IncludeBody = includeBody;
        Title = title;
        Detail = detail;
        ErrorCode = errorCode;
    }

    public static CardsServiceException Unauthorized(string message)
        => new(StatusCodes.Status401Unauthorized, message, null, false, null, null, null);

    public static CardsServiceException NotFound(string message)
        => new(StatusCodes.Status404NotFound, message, null, false, null, null, null);

    public static CardsServiceException NotFound(string message, bool includeBody, object? body)
        => new(StatusCodes.Status404NotFound, message, body, includeBody, null, null, null);

    public static CardsServiceException BadRequest(string message, bool includeBody)
        => new(StatusCodes.Status400BadRequest, message, message, includeBody, null, null, null);

    public static CardsServiceException Problem(int statusCode, string title, string detail, string errorCode)
        => new(statusCode, title, null, false, title, detail, errorCode);
}
