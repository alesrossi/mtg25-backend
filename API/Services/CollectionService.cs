using System.Globalization;
using API.Dtos.Cards;
using API.Dtos.Collections;
using API.Endpoints.Collections;
using API.Helpers;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Core.Specifications;
using Microsoft.AspNetCore.Identity;
using static API.Helpers.CollectionValueCalculator;

namespace API.Services;

public interface ICollectionService
{
    Task<Collection> GetCollectionByIdAsync(int id, string userId, CancellationToken cancellationToken = default);
    Task<object> GetCardsFromCollectionAsync(
        int id,
        string userId,
        EntitySpecParams entityParams,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Collection>> GetCollectionsForUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<Collection> CreateCollectionAsync(NewCollectionDto collectionDto, string userId, CancellationToken cancellationToken = default);
    Task<Collection> UpdateCollectionAsync(int id, NewCollectionDto collectionDto, string userId, CancellationToken cancellationToken = default);
    Task DeleteCollectionAsync(int id, string userId, CancellationToken cancellationToken = default);
    Task<CollectionImportResult> ImportCardsAsync(int id, string userId, IFormFile file, CollectionsEndpoints.ImportSource source, CancellationToken cancellationToken = default);
    Task<int> MassDeleteCardsAsync(int id, string userId, List<int>? cardIds, CancellationToken cancellationToken = default);
}

public sealed class CollectionService : ICollectionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly CardDataService _cardDataService;
    private readonly IUserSettingsService _userSettingsService;
    private readonly UserManager<AppUser> _userManager;

    public CollectionService(
        IUnitOfWork unitOfWork,
        IValidationService validationService,
        CardDataService cardDataService,
        IUserSettingsService userSettingsService,
        UserManager<AppUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _cardDataService = cardDataService;
        _userSettingsService = userSettingsService;
        _userManager = userManager;
    }

    public async Task<Collection> GetCollectionByIdAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw CollectionServiceException.Unauthorized("Missing user id");
        }

        var collection = await _unitOfWork.Repository<Collection>().GetByIdAsync(id, tracking: false);
        if (collection is null)
        {
            throw CollectionServiceException.NotFound("Collection not found");
        }

        if (collection.OwnerId != userId)
        {
            throw CollectionServiceException.Unauthorized("Unauthorized");
        }

        return collection;
    }

    public async Task<object> GetCardsFromCollectionAsync(
        int id,
        string userId,
        EntitySpecParams entityParams,
        CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw CollectionServiceException.Unauthorized("Missing user id");
        }

        var collection = await _unitOfWork.Repository<Collection>().GetByIdAsync(id, tracking: false);
        if (collection is null)
        {
            throw CollectionServiceException.NotFound("Collection not found");
        }
        if (collection.OwnerId != userId)
        {
            throw CollectionServiceException.Unauthorized("Unauthorized");
        }

        if (!string.IsNullOrEmpty(entityParams.GroupBy))
        {
            return await GetGroupedCardsFromCollectionAsync(id, entityParams);
        }

        var sortByCurrentPrice = SortsByCurrentPrice(entityParams.Sort);
        var listingSpec = sortByCurrentPrice
            ? new CardsWithParamsSpecification(entityParams, id, applySorting: false, applyPaging: false)
            : new CardsWithParamsSpecification(entityParams, id);

        var cards = await _unitOfWork.Repository<Card>().ListAsync(listingSpec, tracking: false) ?? Array.Empty<Card>();

        var marketProvider = await _userSettingsService.GetMarketProviderAsync(userId);
        var mappedCards = new List<ExtensiveCardDto>(cards.Count);
        foreach (var card in cards)
        {
            var (price, resolvedProvider) = ResolveMarketPrice(card, _cardDataService, marketProvider);
            mappedCards.Add(MapToDto(card, price, resolvedProvider));
        }

        int size;
        if (sortByCurrentPrice)
        {
            var skip = entityParams.PageSize * (entityParams.PageIndex - 1);
            mappedCards = SortByCurrentPrice(mappedCards, entityParams.Sort)
                .Skip(skip)
                .Take(entityParams.PageSize)
                .ToList();
            size = cards.Count;
        }
        else
        {
            size = await _unitOfWork.Repository<Card>().CountAsync(listingSpec);
        }

        return new Pagination<ExtensiveCardDto>(entityParams.PageIndex, entityParams.PageSize, size, mappedCards);
    }

    public async Task<IReadOnlyList<Collection>> GetCollectionsForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw CollectionServiceException.Unauthorized("Missing user id");
        }

        var collections = await _unitOfWork.Repository<Collection>().ListAsync(new CollectionWithOwnerSpecification(userId), tracking: false);
        return collections ?? [];
    }

    public async Task<Collection> CreateCollectionAsync(NewCollectionDto collectionDto, string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw CollectionServiceException.Unauthorized("Missing user id");
        }

        var (isValid, errors) = _validationService.ValidateModel(collectionDto);
        if (!isValid)
        {
            throw CollectionServiceException.ValidationFailed(errors, "Validation failed");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            throw CollectionServiceException.Unauthorized("User not found");
        }

        var collection = new Collection
        {
            Name = collectionDto.Name,
            Color = collectionDto.Color,
            NumberOfCards = 0,
            TotalPrice = 0,
            OwnerId = user.Id
        };
        _unitOfWork.Repository<Collection>().Add(collection);
        await _unitOfWork.Complete();

        return collection;
    }

    public async Task<Collection> UpdateCollectionAsync(int id, NewCollectionDto collectionDto, string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw CollectionServiceException.Unauthorized("Missing user id");
        }

        var (isValid, errors) = _validationService.ValidateModel(collectionDto);
        if (!isValid)
        {
            throw CollectionServiceException.ValidationFailed(errors, "Validation failed");
        }

        var collection = await _unitOfWork.Repository<Collection>().GetByIdAsync(id);
        if (collection is null)
        {
            throw CollectionServiceException.NotFound("Collection not found");
        }
        if (collection.OwnerId != userId)
        {
            throw CollectionServiceException.Unauthorized("Unauthorized access");
        }

        collection.Name = collectionDto.Name;
        collection.Color = collectionDto.Color;
        _unitOfWork.Repository<Collection>().Update(collection);
        await _unitOfWork.Complete();

        return collection;
    }

    public async Task DeleteCollectionAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw CollectionServiceException.Unauthorized("Missing user id");
        }

        var collection = await _unitOfWork.Repository<Collection>().GetByIdAsync(id);
        if (collection is null)
        {
            throw CollectionServiceException.NotFound("Collection not found");
        }
        if (collection.OwnerId != userId)
        {
            throw CollectionServiceException.Unauthorized("Unauthorized");
        }

        _unitOfWork.Repository<Collection>().Delete(collection);
        await _unitOfWork.Complete();
    }

    public async Task<CollectionImportResult> ImportCardsAsync(int id, string userId, IFormFile file, CollectionsEndpoints.ImportSource source, CancellationToken cancellationToken = default)
    {
        try
        {
            if (userId is null)
            {
                throw CollectionServiceException.Unauthorized("Missing user id");
            }

            if (file.Length <= 0)
            {
                throw CollectionServiceException.BadRequest("No file uploaded", includeBody: true);
            }
            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                throw CollectionServiceException.BadRequest("File must be csv", includeBody: true);
            }
            if (file.Length > 10 * 1024 * 1024)
            {
                throw CollectionServiceException.BadRequest("File is too large", includeBody: true);
            }

            var marketProvider = await _userSettingsService.GetMarketProviderAsync(userId);
            var userCurrency = _userSettingsService.ResolveCurrency(marketProvider);
            var importResult = source switch
            {
                CollectionsEndpoints.ImportSource.Manabox => await CollectionHelpers.ProcessCsvFIle(file, _cardDataService, id, marketProvider, userCurrency),
                CollectionsEndpoints.ImportSource.Moxfield => await CollectionHelpers.ProcessMoxfieldCsvFile(file, _cardDataService, id, marketProvider, userCurrency),
                CollectionsEndpoints.ImportSource.Goldfish => await CollectionHelpers.ProcessGoldfishCsvFile(file, _cardDataService, id, marketProvider, userCurrency),
                CollectionsEndpoints.ImportSource.Archidekt => await CollectionHelpers.ProcessArchidektCsvFile(file, _cardDataService, id, marketProvider, userCurrency),
                CollectionsEndpoints.ImportSource.Dragonshield => await CollectionHelpers.ProcessDragonshieldCsvFile(file, _cardDataService, id, marketProvider, userCurrency),
                CollectionsEndpoints.ImportSource.Delver => await CollectionHelpers.ProcessDelverCsvFile(file, _cardDataService, id, marketProvider, userCurrency),
                _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unsupported import source.")
            };

            var collection = await _unitOfWork.Repository<Collection>().GetByIdAsync(id);
            if (collection is null)
            {
                throw CollectionServiceException.NotFound("Collection not found");
            }
            if (collection.OwnerId != userId)
            {
                throw CollectionServiceException.Unauthorized("Unauthorized");
            }

            if (importResult.Cards.Count == 0)
            {
                var errors = importResult.Errors.Any()
                    ? importResult.Errors
                    : ["CSV file did not contain any valid cards."];

                throw CollectionServiceException.BadRequest(new
                {
                    cards = importResult.Cards,
                    errors,
                    skippedLines = importResult.SkippedLines
                }, includeBody: true);
            }

            var importedCount = importResult.Cards.Sum(card => card.Quantity);
            var existingCards = await _unitOfWork.Repository<Card>()
                .ListAsync(new CardsForCollectionSpecification(id), tracking: false) ?? [];
            var existingTotal = existingCards.Sum(card => card.PurchasePrice * Math.Max(0, card.Quantity));
            var importedTotal = importResult.Cards.Sum(card => card.PurchasePrice * Math.Max(0, card.Quantity));

            collection.NumberOfCards += importedCount;
            collection.TotalPrice = Math.Round(existingTotal + importedTotal, 2, MidpointRounding.AwayFromZero);

            _unitOfWork.Repository<Card>().Add(importResult.Cards);
            _unitOfWork.Repository<Collection>().Update(collection);
            await _unitOfWork.Complete();

            return importResult;
        }
        catch (CollectionServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw CollectionServiceException.InternalError(ex.Message);
        }
    }

    public async Task<int> MassDeleteCardsAsync(int id, string userId, List<int>? cardIds, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw CollectionServiceException.Unauthorized("Missing user id");
        }

        var collection = await _unitOfWork.Repository<Collection>().GetByIdAsync(id);
        if (collection is null)
        {
            throw CollectionServiceException.NotFound("Collection not found");
        }
        if (collection.OwnerId != userId)
        {
            throw CollectionServiceException.Unauthorized("Unauthorized");
        }

        if (cardIds is null || cardIds.Count == 0)
        {
            throw CollectionServiceException.BadRequest("No card ids provided.", includeBody: true);
        }

        var cardsToDelete = await _unitOfWork.Repository<Card>().ListAsync(new CardsByIdsSpecification(cardIds, id));
        if (cardsToDelete is null || cardsToDelete.Count == 0)
        {
            throw CollectionServiceException.NotFound("Cards not found");
        }

        var totalRemovedQuantity = 0;
        var totalRemovedValue = 0d;
        foreach (var card in cardsToDelete)
        {
            _unitOfWork.Repository<Card>().Delete(card);
            totalRemovedQuantity += Math.Max(0, card.Quantity);
            totalRemovedValue += CalculateCardValue(card.PurchasePrice, card.Quantity);
        }

        collection.NumberOfCards = Math.Max(0, collection.NumberOfCards - totalRemovedQuantity);
        collection.TotalPrice = ApplyTotalPriceDelta(collection.TotalPrice, -totalRemovedValue);
        _unitOfWork.Repository<Collection>().Update(collection);

        await _unitOfWork.Complete();
        return cardsToDelete.Count;
    }

    private async Task<GroupedCardsPaginationDto> GetGroupedCardsFromCollectionAsync(
        int collectionId,
        EntitySpecParams entityParams)
    {
        var spec = new CardsWithParamsSpecification(entityParams, collectionId);
        var allCards = await _unitOfWork.Repository<Card>().ListAsync(spec, tracking: false);

        if (allCards == null || !allCards.Any())
        {
            return new GroupedCardsPaginationDto
            {
                PageIndex = entityParams.PageIndex,
                PageSize = entityParams.PageSize,
                TotalGroups = 0,
                TotalCards = 0,
                Groups = []
            };
        }

        var groupedCards = entityParams.GroupBy?.ToLower() switch
        {
            "setname" => allCards.GroupBy(c => c.SetName).Select(g => new GroupedCardsDto
            {
                GroupKey = g.Key,
                Count = g.Count(),
                Cards = g.ToList()
            }).ToList(),
            "setcode" => allCards.GroupBy(c => c.SetCode).Select(g => new GroupedCardsDto
            {
                GroupKey = g.Key,
                Count = g.Count(),
                Cards = g.ToList()
            }).ToList(),
            "rarity" => allCards.GroupBy(c => c.Rarity).Select(g => new GroupedCardsDto
            {
                GroupKey = g.Key,
                Count = g.Count(),
                Cards = g.ToList()
            }).ToList(),
            "condition" => allCards.GroupBy(c => c.Condition.ToString()).Select(g => new GroupedCardsDto
            {
                GroupKey = g.Key,
                Count = g.Count(),
                Cards = g.ToList()
            }).ToList(),
            "language" => allCards.GroupBy(c => c.Language).Select(g => new GroupedCardsDto
            {
                GroupKey = g.Key,
                Count = g.Count(),
                Cards = g.ToList()
            }).ToList(),
            _ => allCards.GroupBy(c => c.Name).Select(g => new GroupedCardsDto
            {
                GroupKey = g.Key,
                Count = g.Count(),
                Cards = g.ToList()
            }).ToList()
        };

        var totalGroups = groupedCards.Count;
        var totalCards = allCards.Count;

        var paginatedGroups = groupedCards
            .Skip(entityParams.PageSize * (entityParams.PageIndex - 1))
            .Take(entityParams.PageSize)
            .ToList();

        return new GroupedCardsPaginationDto
        {
            PageIndex = entityParams.PageIndex,
            PageSize = entityParams.PageSize,
            TotalGroups = totalGroups,
            TotalCards = totalCards,
            Groups = paginatedGroups
        };
    }

    private static bool SortsByCurrentPrice(string? sort) =>
        string.Equals(sort, "currentPriceAsc", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(sort, "currentPriceDesc", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<ExtensiveCardDto> SortByCurrentPrice(IEnumerable<ExtensiveCardDto> cards, string? sort)
    {
        var ordered = cards.OrderBy(c => c.Price.HasValue ? 0 : 1);
        var descending = string.Equals(sort, "currentPriceDesc", StringComparison.OrdinalIgnoreCase);
        return descending
            ? ordered.ThenByDescending(c => c.Price ?? double.MinValue)
            : ordered.ThenBy(c => c.Price ?? double.MaxValue);
    }

    private static (double? price, MarketProvider? provider) ResolveMarketPrice(Card card, CardDataService cds, MarketProvider preferredProvider)
    {
        if (!cds.CardDataById.TryGetValue(card.ScryfallId, out var marketData) || marketData?.Prices is null)
        {
            return (null, preferredProvider);
        }

        var prices = marketData.Prices;
        foreach (var provider in EnumerateProviders(preferredProvider))
        {
            var selected = provider == MarketProvider.Mkm
                ? (card.IsFoil ? prices.EurFoil : prices.Eur)
                : (card.IsFoil ? prices.UsdFoil : prices.Usd);

            var parsed = TryParsePrice(selected);
            if (parsed.HasValue)
            {
                return (parsed, provider);
            }
        }

        return (null, preferredProvider);
    }

    private static IEnumerable<MarketProvider> EnumerateProviders(MarketProvider preferred)
    {
        yield return preferred;
        yield return preferred == MarketProvider.Mkm ? MarketProvider.Tcg : MarketProvider.Mkm;
    }

    private static double? TryParsePrice(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
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
}

public sealed class CollectionServiceException : Exception
{
    public int StatusCode { get; }
    public object? Body { get; }
    public bool IncludeBody { get; }

    private CollectionServiceException(int statusCode, string? message, object? body, bool includeBody)
        : base(message ?? string.Empty)
    {
        StatusCode = statusCode;
        Body = body;
        IncludeBody = includeBody;
    }

    public static CollectionServiceException Unauthorized(string message)
        => new(StatusCodes.Status401Unauthorized, message, null, false);

    public static CollectionServiceException NotFound(string message)
        => new(StatusCodes.Status404NotFound, message, null, false);

    public static CollectionServiceException NotFound(string message, bool includeBody, object? body)
        => new(StatusCodes.Status404NotFound, message, body, includeBody);

    public static CollectionServiceException BadRequest(object? body, bool includeBody)
        => new(StatusCodes.Status400BadRequest, "Bad request", body, includeBody);

    public static CollectionServiceException BadRequest(string message, bool includeBody)
        => new(StatusCodes.Status400BadRequest, message, message, includeBody);

    public static CollectionServiceException ValidationFailed(Dictionary<string, string[]> errors, string message)
        => new(StatusCodes.Status400BadRequest, message, new ValidationErrorsResponse(errors), true);

    public static CollectionServiceException InternalError(string message)
        => new(StatusCodes.Status500InternalServerError, message, null, false);
}
