using System.Globalization;
using API.Dtos.Binders;
using API.Dtos.Cards;
using API.Helpers;
using Core.Enums;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Core.Specifications;
using Microsoft.EntityFrameworkCore;

namespace API.Services;

public interface IBindersService
{
    Task<IReadOnlyList<BinderSummaryDto>> GetBindersAsync(string userId, CancellationToken cancellationToken = default);
    Task<BinderDto> GetBinderByIdAsync(int id, string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BinderCardDto>> GetBinderCardsAsync(int binderId, string userId, CancellationToken cancellationToken = default);
    Task<BinderCardDto> GetBinderCardByIdAsync(int binderId, int binderCardId, string userId, CancellationToken cancellationToken = default);
    Task<BinderDto> CreateBinderAsync(CreateBinderDto createDto, string userId, CancellationToken cancellationToken = default);
    Task<BinderDto> UpdateBinderAsync(int id, UpdateBinderDto updateDto, string userId, CancellationToken cancellationToken = default);
    Task DeleteBinderAsync(int id, string userId, CancellationToken cancellationToken = default);
    Task<List<BinderCardDto>> CreateBinderCardsAsync(int binderId, List<CreateBinderCardDto> createDtoList, string userId, CancellationToken cancellationToken = default);
    Task<BinderCardDto> UpdateBinderCardAsync(int binderId, int binderCardId, UpdateBinderCardDto updateDto, string userId, CancellationToken cancellationToken = default);
    Task DeleteBinderCardAsync(int binderId, int binderCardId, string userId, CancellationToken cancellationToken = default);
}

public sealed class BindersService : IBindersService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly IUserSettingsService _userSettingsService;
    private readonly CardDataService _cardDataService;
    private readonly ITeamService _teamService;

    public BindersService(
        IUnitOfWork unitOfWork,
        IValidationService validationService,
        IUserSettingsService userSettingsService,
        CardDataService cardDataService,
        ITeamService teamService)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _userSettingsService = userSettingsService;
        _cardDataService = cardDataService;
        _teamService = teamService;
    }

    public async Task<IReadOnlyList<BinderSummaryDto>> GetBindersAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw BindersServiceException.Unauthorized("Errors.Binders.MissingUserId");
        }

        var spec = new TradeBindersWithOwnerSpecification(userId, includeCards: true);
        var binders = await _unitOfWork.Repository<TradeBinder>().ListAsync(spec, tracking: false) ?? Array.Empty<TradeBinder>();

        if (binders.Count > 0)
        {
            var binderIds = binders.Select(b => b.Id).ToArray();
            var binderCards = await _unitOfWork.Repository<BinderCard>()
                .ListAsync(new BinderCardsByBinderIdsSpecification(binderIds), tracking: false) ?? [];
            var cardsGrouped = binderCards
                .GroupBy(card => card.TradeBinderId)
                .ToDictionary(group => group.Key, group => group.ToList());

            foreach (var binder in binders)
            {
                if (cardsGrouped.TryGetValue(binder.Id, out var cards))
                {
                    binder.BinderCards = cards;
                }
            }
        }

        return binders.Select(MapToSummaryDto).ToList();
    }

    public async Task<BinderDto> GetBinderByIdAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        var binder = await _unitOfWork.Repository<TradeBinder>().Query
            .AsNoTracking()
            .Include(b => b.Team)
            .Include(b => b.BinderCards)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (binder == null)
        {
            throw BindersServiceException.NotFound("Errors.Binders.NotFound");
        }

        var isOwner = !string.IsNullOrEmpty(userId) && binder.OwnerId == userId;
        var hasTeamAccess = binder.TeamId.HasValue && await _teamService.HasTeamAccessAsync(binder.TeamId.Value, userId, TeamRole.Member);
        if (!isOwner && !hasTeamAccess && !binder.IsPublic)
        {
            throw BindersServiceException.Unauthorized("Errors.Binders.Unauthorized");
        }

        var marketProvider = !string.IsNullOrEmpty(userId)
            ? await _userSettingsService.GetMarketProviderAsync(userId, cancellationToken)
            : MarketProvider.Mkm;
        var cards = await _unitOfWork.Repository<BinderCard>()
            .ListAsync(new BinderCardsWithBinderIdSpecification(binder.Id), tracking: false) ?? [];
        var pricedCards = MapBinderCardsWithMarketData(cards, marketProvider, _userSettingsService, _cardDataService);

        var dto = MapToDto(binder, cards);
        dto.Cards = pricedCards;
        dto.CardsCount = pricedCards.Count;
        return dto;
    }

    public async Task<IReadOnlyList<BinderCardDto>> GetBinderCardsAsync(int binderId, string userId, CancellationToken cancellationToken = default)
    {
        var binder = await _unitOfWork.Repository<TradeBinder>().GetByIdAsync(binderId, tracking: false);
        if (binder == null)
        {
            throw BindersServiceException.NotFound("Errors.Binders.NotFound");
        }

        var isOwner = !string.IsNullOrEmpty(userId) && binder.OwnerId == userId;
        if (!isOwner && !binder.IsPublic)
        {
            throw BindersServiceException.Unauthorized("Errors.Binders.Unauthorized");
        }

        var marketProvider = !string.IsNullOrEmpty(userId)
            ? await _userSettingsService.GetMarketProviderAsync(userId, cancellationToken)
            : MarketProvider.Mkm;
        var cards = await _unitOfWork.Repository<BinderCard>()
            .ListAsync(new BinderCardsWithBinderIdSpecification(binder.Id), tracking: false) ?? [];

        return MapBinderCardsWithMarketData(cards, marketProvider, _userSettingsService, _cardDataService);
    }

    public async Task<BinderCardDto> GetBinderCardByIdAsync(int binderId, int binderCardId, string userId, CancellationToken cancellationToken = default)
    {
        var binder = await _unitOfWork.Repository<TradeBinder>().GetByIdAsync(binderId, tracking: false);
        if (binder == null)
        {
            throw BindersServiceException.NotFound("Errors.Binders.NotFound");
        }

        if (string.IsNullOrEmpty(userId))
        {
            throw BindersServiceException.Unauthorized("Errors.Binders.MissingUserId");
        }

        var isOwner = binder.OwnerId == userId;
        if (!isOwner && !binder.IsPublic)
        {
            throw BindersServiceException.Unauthorized("Errors.Binders.Unauthorized");
        }

        var binderCard = await _unitOfWork.Repository<BinderCard>()
            .GetEntityWithSpec(new BinderCardWithBinderSpecification(binderCardId), tracking: false);
        if (binderCard == null || binderCard.TradeBinderId != binder.Id)
        {
            throw BindersServiceException.NotFound("Errors.Binders.CardNotFound");
        }

        var marketProvider = await _userSettingsService.GetMarketProviderAsync(userId, cancellationToken);
        return MapBinderCardsWithMarketData([binderCard], marketProvider, _userSettingsService, _cardDataService).First();
    }

    public async Task<BinderDto> CreateBinderAsync(CreateBinderDto createDto, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw BindersServiceException.Unauthorized("Errors.Binders.MissingUserId");
        }

        if (createDto.TeamId.HasValue)
        {
            if (!await _teamService.HasTeamAccessAsync(createDto.TeamId.Value, userId, TeamRole.Admin))
            {
                throw BindersServiceException.Unauthorized("Errors.Binders.Unauthorized");
            }
        }

        var (isValid, errors) = _validationService.ValidateModel(createDto);
        if (!isValid)
        {
            throw BindersServiceException.ValidationFailed(errors, "Errors.Binders.ValidationFailed");
        }

        var binder = new TradeBinder
        {
            Name = createDto.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(createDto.Description) ? null : createDto.Description.Trim(),
            IsPublic = createDto.IsPublic,
            OwnerId = userId,
            TeamId = createDto.TeamId
        };

        _unitOfWork.Repository<TradeBinder>().Add(binder);
        await _unitOfWork.Complete();

        var createdBinder = await _unitOfWork.Repository<TradeBinder>().Query
            .AsNoTracking()
            .Include(b => b.Team)
            .FirstOrDefaultAsync(b => b.Id == binder.Id, cancellationToken);

        return MapToDto(createdBinder ?? binder, []);
    }

    public async Task<BinderDto> UpdateBinderAsync(int id, UpdateBinderDto updateDto, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw BindersServiceException.Unauthorized("Errors.Binders.MissingUserId");
        }

        var (isValid, errors) = _validationService.ValidateModel(updateDto);
        if (!isValid)
        {
            throw BindersServiceException.ValidationFailed(errors, "Errors.Binders.ValidationFailed");
        }

        var binder = await _unitOfWork.Repository<TradeBinder>().GetByIdAsync(id);
        if (binder == null)
        {
            throw BindersServiceException.NotFound("Errors.Binders.NotFound");
        }

        if (binder.TeamId.HasValue)
        {
            if (!await _teamService.HasTeamAccessAsync(binder.TeamId.Value, userId, TeamRole.Admin))
            {
                throw BindersServiceException.Unauthorized("Errors.Binders.Unauthorized");
            }
        }
        else if (binder.OwnerId != userId)
        {
            throw BindersServiceException.Unauthorized("Errors.Binders.Unauthorized");
        }

        binder.Name = updateDto.Name.Trim();
        binder.Description = string.IsNullOrWhiteSpace(updateDto.Description) ? null : updateDto.Description.Trim();
        binder.IsPublic = updateDto.IsPublic;

        _unitOfWork.Repository<TradeBinder>().Update(binder);
        await _unitOfWork.Complete();

        var updated = await _unitOfWork.Repository<TradeBinder>().Query
            .AsNoTracking()
            .Include(b => b.Team)
            .Include(b => b.BinderCards)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken) ?? binder;
        var cards = await _unitOfWork.Repository<BinderCard>()
            .ListAsync(new BinderCardsWithBinderIdSpecification(binder.Id)) ?? [];

        return MapToDto(updated, cards);
    }

    public async Task DeleteBinderAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw BindersServiceException.Unauthorized("Errors.Binders.MissingUserId");
        }

        var binder = await _unitOfWork.Repository<TradeBinder>().GetByIdAsync(id);
        if (binder == null)
        {
            throw BindersServiceException.NotFound("Errors.Binders.NotFound");
        }

        if (binder.TeamId.HasValue)
        {
            if (!await _teamService.HasTeamAccessAsync(binder.TeamId.Value, userId, TeamRole.Admin))
            {
                throw BindersServiceException.Unauthorized("Errors.Binders.Unauthorized");
            }
        }
        else if (binder.OwnerId != userId)
        {
            throw BindersServiceException.Unauthorized("Errors.Binders.Unauthorized");
        }

        _unitOfWork.Repository<TradeBinder>().Delete(binder);
        await _unitOfWork.Complete();
    }

    public async Task<List<BinderCardDto>> CreateBinderCardsAsync(
        int binderId,
        List<CreateBinderCardDto> createDtoList,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var binder = await EnsureBinderAccessAsync(binderId, userId, requireOwner: true);
        var cardList = new List<BinderCardDto>();

        foreach (var createDto in createDtoList)
        {
            var (isValid, errors) = _validationService.ValidateModel(createDto);
            if (!isValid)
            {
                throw BindersServiceException.ValidationFailed(errors, "Errors.Binders.ValidationFailed");
            }

            var card = await _unitOfWork.Repository<Card>().GetByIdAsync(createDto.CardId);
            if (card == null)
            {
                throw BindersServiceException.BadRequest(new { errors = new { CardId = new[] { "Card not found." } } }, true);
            }

            var collection = await _unitOfWork.Repository<Collection>().GetByIdAsync(card.CollectionId);
            if (collection == null || collection.OwnerId != binder.OwnerId)
            {
                throw BindersServiceException.Unauthorized("Errors.Binders.Unauthorized");
            }

            if (createDto.QuantityToTrade > card.Quantity)
            {
                throw BindersServiceException.BadRequest(new
                {
                    errors = new
                    {
                        QuantityToTrade = new[] { "Quantity to trade exceeds available card quantity." }
                    }
                }, true);
            }

            var binderCard = new BinderCard
            {
                TradeBinderId = binder.Id,
                CardId = card.Id,
                Name = card.Name,
                QuantityToTrade = createDto.QuantityToTrade,
                Notes = string.IsNullOrWhiteSpace(createDto.Notes) ? null : createDto.Notes.Trim()
            };

            _unitOfWork.Repository<BinderCard>().Add(binderCard);
            await _unitOfWork.Complete();

            var hydratedCard = await _unitOfWork.Repository<BinderCard>()
                .GetEntityWithSpec(new BinderCardWithBinderSpecification(binderCard.Id)) ?? binderCard;
            cardList.Add(MapBinderCardToDto(hydratedCard));
        }

        return cardList;
    }

    public async Task<BinderCardDto> UpdateBinderCardAsync(
        int binderId,
        int binderCardId,
        UpdateBinderCardDto updateDto,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var binder = await EnsureBinderAccessAsync(binderId, userId, requireOwner: true);

        var (isValid, errors) = _validationService.ValidateModel(updateDto);
        if (!isValid)
        {
            throw BindersServiceException.ValidationFailed(errors, "Errors.Binders.ValidationFailed");
        }

        var binderCard = await _unitOfWork.Repository<BinderCard>().GetEntityWithSpec(new BinderCardWithBinderSpecification(binderCardId));
        if (binderCard == null || binderCard.TradeBinderId != binder.Id)
        {
            throw BindersServiceException.NotFound("Errors.Binders.CardNotFound");
        }

        if (binderCard.TradeBinder.OwnerId != binder.OwnerId)
        {
            throw BindersServiceException.Unauthorized("Errors.Binders.Unauthorized");
        }

        var card = binderCard.Card ?? await _unitOfWork.Repository<Card>().GetByIdAsync(binderCard.CardId);
        if (card == null)
        {
            throw BindersServiceException.BadRequest(new { errors = new { CardId = new[] { "Card not found." } } }, true);
        }

        if (updateDto.QuantityToTrade > card.Quantity)
        {
            throw BindersServiceException.BadRequest(new
            {
                errors = new
                {
                    QuantityToTrade = new[] { "Quantity to trade exceeds available card quantity." }
                }
            }, true);
        }

        binderCard.QuantityToTrade = updateDto.QuantityToTrade;
        binderCard.Notes = string.IsNullOrWhiteSpace(updateDto.Notes) ? null : updateDto.Notes.Trim();

        _unitOfWork.Repository<BinderCard>().Update(binderCard);
        await _unitOfWork.Complete();

        var updated = await _unitOfWork.Repository<BinderCard>()
            .GetEntityWithSpec(new BinderCardWithBinderSpecification(binderCardId)) ?? binderCard;

        return MapBinderCardToDto(updated);
    }

    public async Task DeleteBinderCardAsync(int binderId, int binderCardId, string userId, CancellationToken cancellationToken = default)
    {
        var binder = await EnsureBinderAccessAsync(binderId, userId, requireOwner: true);

        var binderCard = await _unitOfWork.Repository<BinderCard>().GetEntityWithSpec(new BinderCardWithBinderSpecification(binderCardId));
        if (binderCard == null || binderCard.TradeBinderId != binder.Id)
        {
            throw BindersServiceException.NotFound("Errors.Binders.CardNotFound");
        }

        _unitOfWork.Repository<BinderCard>().Delete(binderCard);
        await _unitOfWork.Complete();
    }

    private async Task<TradeBinder> EnsureBinderAccessAsync(int binderId, string userId, bool requireOwner)
    {
        var binder = await _unitOfWork.Repository<TradeBinder>().GetByIdAsync(binderId);
        if (binder == null)
        {
            throw BindersServiceException.NotFound("Errors.Binders.NotFound");
        }

        var isOwner = !string.IsNullOrEmpty(userId) && binder.OwnerId == userId;
        if (requireOwner && !isOwner)
        {
            throw BindersServiceException.Unauthorized("Errors.Binders.Unauthorized");
        }

        return binder;
    }

    private static BinderSummaryDto MapToSummaryDto(TradeBinder binder)
    {
        var cards = binder.BinderCards?.ToList() ?? [];

        return new BinderSummaryDto
        {
            Id = binder.Id,
            Name = binder.Name,
            Description = binder.Description,
            IsPublic = binder.IsPublic,
            CardsCount = cards.Count,
            TotalPrice = CalculateBinderTotalPrice(cards)
        };
    }

    private static BinderDto MapToDto(TradeBinder binder, IEnumerable<BinderCard>? binderCards = null)
    {
        var cards = binderCards?.ToList() ?? binder.BinderCards?.ToList() ?? [];
        var cardDtos = cards.Select(card => MapBinderCardToDto(card)).ToList();

        return new BinderDto
        {
            Id = binder.Id,
            Name = binder.Name,
            Description = binder.Description,
            IsPublic = binder.IsPublic,
            OwnerId = binder.OwnerId,
            CardsCount = cardDtos.Count,
            TotalPrice = CalculateBinderTotalPrice(cards),
            Cards = cardDtos,
            TeamId = binder.TeamId,
            TeamName = binder.Team?.Name
        };
    }

    private static BinderCardDto MapBinderCardToDto(
        BinderCard card,
        double? marketPrice = null,
        double? totalValue = null,
        MarketProvider? marketProvider = null,
        Currency? currency = null)
    {
        return new BinderCardDto
        {
            Id = card.Id,
            TradeBinderId = card.TradeBinderId,
            CardId = card.CardId,
            Card = card.Card!,
            Name = card.Name,
            QuantityToTrade = card.QuantityToTrade,
            MaxQuantityToTrade = card.Card?.Quantity ?? card.QuantityToTrade,
            Notes = card.Notes,
            ImageUrl = card.Card?.ImageUrl,
            SetCode = card.Card?.SetCode,
            SetName = card.Card?.SetName,
            CollectorNumber = card.Card?.CollectorNumber,
            Rarity = card.Card?.Rarity,
            MarketPrice = marketPrice,
            TotalValue = totalValue,
            MarketProvider = marketProvider,
            Currency = currency
        };
    }

    private static double CalculateBinderTotalPrice(IEnumerable<BinderCard> cards)
    {
        var total = 
            (from card in cards let purchasePrice = card.Card?.PurchasePrice 
                                                    ?? 0 let quantity = Math.Max(0, card.QuantityToTrade) 
            select CollectionValueCalculator.CalculateCardValue(purchasePrice, quantity)).Sum();

        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private static List<BinderCardDto> MapBinderCardsWithMarketData(
        IEnumerable<BinderCard> cards,
        MarketProvider preferredProvider,
        IUserSettingsService userSettingsService,
        CardDataService cardDataService)
    {
        var pricedCards = new List<BinderCardDto>();
        foreach (var card in cards)
        {
            var (marketPrice, provider) = ResolveMarketPrice(card, cardDataService, preferredProvider);
            var quantity = Math.Max(0, card.QuantityToTrade);
            var totalValue = marketPrice.HasValue
                ? Math.Round(marketPrice.Value * quantity, 2, MidpointRounding.AwayFromZero)
                : (double?)null;
            var currency = provider.HasValue
                ? userSettingsService.ResolveCurrency(provider.Value)
                : (Currency?)null;

            pricedCards.Add(MapBinderCardToDto(card, marketPrice, totalValue, provider, currency));
        }

        return pricedCards;
    }

    private static (double? Price, MarketProvider? Provider) ResolveMarketPrice(
        BinderCard card,
        CardDataService cardDataService,
        MarketProvider preferredProvider)
    {
        var marketData = TryResolveCardData(card, cardDataService);
        if (marketData?.Prices is null)
        {
            return (null, null);
        }

        var isFoil = card.Card?.IsFoil ?? false;
        foreach (var provider in EnumerateProviders(preferredProvider))
        {
            var selected = provider == MarketProvider.Mkm
                ? (isFoil ? marketData.Prices.EurFoil : marketData.Prices.Eur)
                : (isFoil ? marketData.Prices.UsdFoil : marketData.Prices.Usd);

            var parsed = TryParsePrice(selected);
            if (parsed.HasValue)
            {
                return (parsed.Value, provider);
            }
        }

        return (null, null);
    }

    private static ScryfallCardDto? TryResolveCardData(BinderCard card, CardDataService cardDataService)
    {
        if (card.Card is not null)
        {
            if (!string.IsNullOrWhiteSpace(card.Card.ScryfallId)
                && cardDataService.CardDataById.TryGetValue(card.Card.ScryfallId, out var byId))
            {
                return byId;
            }

            if (!string.IsNullOrWhiteSpace(card.Card.Name)
                && cardDataService.CardDataByName.TryGetValue(card.Card.Name, out var byName))
            {
                return byName;
            }
        }

        if (!string.IsNullOrWhiteSpace(card.Name)
            && cardDataService.CardDataByName.TryGetValue(card.Name, out var byBinderName))
        {
            return byBinderName;
        }

        return null;
    }

    private static IEnumerable<MarketProvider> EnumerateProviders(MarketProvider preferredProvider)
    {
        yield return preferredProvider;
        yield return preferredProvider == MarketProvider.Mkm ? MarketProvider.Tcg : MarketProvider.Mkm;
    }

    private static double? TryParsePrice(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}

public sealed class BindersServiceException : Exception
{
    public int StatusCode { get; }
    public object? Body { get; }
    public bool IncludeBody { get; }

    private BindersServiceException(int statusCode, string? message, object? body, bool includeBody)
        : base(message ?? string.Empty)
    {
        StatusCode = statusCode;
        Body = body;
        IncludeBody = includeBody;
    }

    public static BindersServiceException Unauthorized(string message)
        => new(StatusCodes.Status401Unauthorized, message, null, false);

    public static BindersServiceException NotFound(string message)
        => new(StatusCodes.Status404NotFound, message, null, false);

    public static BindersServiceException NotFound(string message, bool includeBody, object? body)
        => new(StatusCodes.Status404NotFound, message, body, includeBody);

    public static BindersServiceException BadRequest(object? body, bool includeBody)
        => new(StatusCodes.Status400BadRequest, "Bad request", body, includeBody);

    public static BindersServiceException ValidationFailed(Dictionary<string, string[]> errors, string message)
        => new(StatusCodes.Status400BadRequest, message, new ValidationErrorsResponse(errors), true);
}
