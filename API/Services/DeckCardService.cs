using System;
using System.Globalization;
using System.Linq;
using API.Dtos.Cards;
using API.Dtos.Decks;
using API.Logging;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Core.Specifications;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Services;

public class DeckCardService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ILogger<DeckCardService> logger;
    private readonly CardDataService cardDataService;
    private readonly AppIdentityDbContext identityDbContext;

    private const string GetDeckCardsOperation = "DeckCards.Fetch";
    private const string CreateDeckCardOperation = "DeckCards.Create";
    private const string UpdateDeckCardOperation = "DeckCards.Update";
    private const string DeleteDeckCardOperation = "DeckCards.Delete";

    public DeckCardService(IUnitOfWork unitOfWork, ILogger<DeckCardService> logger, CardDataService cardDataService, AppIdentityDbContext identityDbContext)
    {
        this.unitOfWork = unitOfWork;
        this.logger = logger;
        this.cardDataService = cardDataService;
        this.identityDbContext = identityDbContext;
    }

    public async Task<IEnumerable<DeckCardDto>> GetDeckCardsAsync(int deckId, bool maindeckOnly = false, bool sideboardOnly = false, bool? ownedOnly = null)
    {
        using var scope = logger.BeginOperationScope(GetDeckCardsOperation, deckId);
        logger.LogOperationStart(GetDeckCardsOperation, new { deckId, maindeckOnly, sideboardOnly, ownedOnly });

        ISpecification<DeckCard> spec = (maindeckOnly, sideboardOnly) switch
        {
            (true, false) => new DeckCardsWithDeckIdSpecification(deckId, maindeckOnly: true),
            (false, true) => new DeckCardsWithDeckIdSpecification(deckId, maindeckOnly: false, sideboardOnly: true),
            (true, true) => new DeckCardsWithDeckIdSpecification(deckId, maindeckOnly: true, sideboardOnly: true),
            _ => new DeckCardsWithDeckIdSpecification(deckId)
        };
        
        var deckCards = await unitOfWork.Repository<DeckCard>().ListAsync(spec, tracking: false);

        // Get the deck owner for collection lookup
        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId, tracking: false);
        if (deck == null)
        {
            logger.LogOperationWarning(GetDeckCardsOperation, "Deck not found", new { deckId });
            return [];
        }

        var marketProvider = await ResolveMarketProviderAsync(deck.OwnerId);
        
        // Get all scryfall IDs from deck cards
        var ids = (deckCards ?? []).Select(dc => dc.ScryfallId).Distinct().ToList();
        
        // Get user's collections
        var userCollectionsSpec = new CollectionWithOwnerSpecification(deck.OwnerId);
        var userCollections = await unitOfWork.Repository<Collection>().ListAsync(userCollectionsSpec, tracking: false);
        
        // Get all owned cards for all scryfall IDs in one query per collection
        var ownedCardsLookup = new Dictionary<string, List<Card>>();
        
        if (userCollections != null)
        {
            foreach (var collection in userCollections)
            {
                var cardsSpec = new CardsWithParamsSpecification(new EntitySpecParams(), collection.Id);
                var collectionCards = await unitOfWork.Repository<Card>().ListAsync(cardsSpec, tracking: false);
                
                var matchingCards = collectionCards?.Where(c => ids.Contains(c.ScryfallId)).ToList();
                if (matchingCards?.Any() == true)
                {
                    foreach (var card in matchingCards)
                    {
                        if (!ownedCardsLookup.ContainsKey(card.ScryfallId))
                            ownedCardsLookup[card.ScryfallId] = new List<Card>();
                        ownedCardsLookup[card.ScryfallId].Add(card);
                    }
                }
            }
        }
        
        // Map to DTOs using the lookup
        var result = (deckCards ?? []).Select(dc => MapToDtoWithLookup(dc, ownedCardsLookup, marketProvider)).ToList();
        
        // Apply ownership filter if specified
        if (ownedOnly.HasValue)
        {
            result = ownedOnly.Value 
                ? result.Where(dc => dc.IsOwned).ToList()
                : result.Where(dc => !dc.IsOwned).ToList();
        }
        
        logger.LogOperationSuccess(GetDeckCardsOperation, new { deckId, Count = result.Count, ownedOnly });
        return result;
    }

    public async Task<IEnumerable<DeckCardDto>> GetDeckCardsByScryfallIdAsync(string scryfallId, int? deckId = null, string? userId = null)
    {
        const string operation = "DeckCards.FetchByScryfallId";
        var scopeKey = deckId?.ToString() ?? userId ?? scryfallId;
        using var scope = logger.BeginOperationScope(operation, scopeKey);
        logger.LogOperationStart(operation, new { scryfallId = scryfallId, deckId, userId });

        ISpecification<DeckCard> spec = deckId.HasValue
            ? new DeckCardsWithScryfallIdSpecification(scryfallId, deckId.Value)
            : new DeckCardsWithScryfallIdSpecification(scryfallId, userId!);
            
        var deckCards = await unitOfWork.Repository<DeckCard>().ListAsync(spec, tracking: false);

        var marketProvider = deckId.HasValue
            ? await ResolveMarketProviderForDeckAsync(deckId.Value)
            : (!string.IsNullOrEmpty(userId) ? await ResolveMarketProviderAsync(userId) : MarketProvider.Mkm);

        var mapped = deckCards?.Select(dc => MapToDto(dc, marketProvider)).ToList() ?? [];
        logger.LogOperationSuccess(operation, new { scryfallId = scryfallId, Count = mapped.Count });
        return mapped;
    }

    public async Task<DeckCardDto?> GetDeckCardByIdAsync(int id)
    {
        const string operation = "DeckCards.FetchById";
        using var scope = logger.BeginOperationScope(operation, id);
        logger.LogOperationStart(operation, new { id });

        var deckCard = await unitOfWork.Repository<DeckCard>().GetByIdAsync(id, tracking: false);
        if (deckCard == null)
        {
            logger.LogOperationWarning(operation, "Deck card not found", new { id });
            return null;
        }
        
        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckCard.DeckId, tracking: false);
        if (deck == null)
        {
            logger.LogOperationWarning(operation, "Deck missing for deck card", new { deckCard.DeckId, deckCard.Id });
            return null;
        }
        
        var marketProvider = await ResolveMarketProviderAsync(deck.OwnerId);
        var dto = await MapToDtoAsync(deckCard, deck.OwnerId, marketProvider);
        logger.LogOperationSuccess(operation, new { id, deckCard.DeckId });
        return dto;
    }

    public async Task<DeckCardDto> CreateDeckCardAsync(int deckId, CreateDeckCardDto createDto)
    {
        using var scope = logger.BeginOperationScope(CreateDeckCardOperation, deckId);
        logger.LogOperationStart(CreateDeckCardOperation, new { deckId, ScryfallId = createDto.ScryfallId, createDto.Name });

        if (string.IsNullOrWhiteSpace(createDto.ScryfallId) || 
            string.IsNullOrWhiteSpace(createDto.Name) || 
            string.IsNullOrWhiteSpace(createDto.SetCode) ||
            string.IsNullOrWhiteSpace(createDto.ImageUrl))
        {
            logger.LogOperationWarning(CreateDeckCardOperation, "Missing required fields", new { deckId, ScryfallId = createDto.ScryfallId });
            throw new ArgumentException("ScryfallId, Name, SetCode, and ImageUrl are required");
        }

        var requestedQuantity = createDto.MaindeckQuantity + createDto.SideboardQuantity;
        await EnsureCopyLimitAsync(deckId, createDto.Name, createDto.TypeLine, requestedQuantity, null, CreateDeckCardOperation);

        var deckCard = new DeckCard
        {
            DeckId = deckId,
            ScryfallId = createDto.ScryfallId,
            Name = createDto.Name,
            SetCode = createDto.SetCode,
            SetName = createDto.SetName,
            TypeLine = createDto.TypeLine,
            ColorIdentity = NormalizeColorIdentity(createDto.ColorIdentity),
            ImageUrl = createDto.ImageUrl,
            BackImageUrl = createDto.BackImageUrl,
            ArtCrop = createDto.ArtCrop,
            Rarity = createDto.Rarity,
            CollectorNumber = createDto.CollectorNumber,
            MaindeckQuantity = createDto.MaindeckQuantity,
            SideboardQuantity = createDto.SideboardQuantity,
            OwnedCardId = createDto.OwnedCardId
        };

        unitOfWork.Repository<DeckCard>().Add(deckCard);
        await unitOfWork.Complete();

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null)
        {
            logger.LogOperationWarning(CreateDeckCardOperation, "Deck not found after card creation", new { deckId, ScryfallId = createDto.ScryfallId });
            throw new InvalidOperationException("Deck not found");
        }
        
        var marketProvider = await ResolveMarketProviderAsync(deck.OwnerId);
        await UpdateDeckAggregatesAsync(deck, deckCard, marketProvider, previousMaindeck: 0, previousSideboard: 0);
        await UpdateDeckColorIdentityAsync(deck, deckCard.ColorIdentity);
        var dto = await MapToDtoAsync(deckCard, deck.OwnerId, marketProvider);
        logger.LogOperationSuccess(CreateDeckCardOperation, new { deckCard.Id, deckId });
        return dto;
    }

    public async Task<DeckCardDto?> UpdateDeckCardAsync(int id, UpdateDeckCardDto updateDto)
    {
        using var scope = logger.BeginOperationScope(UpdateDeckCardOperation, id);
        logger.LogOperationStart(UpdateDeckCardOperation, new { id });

        var deckCard = await unitOfWork.Repository<DeckCard>().GetByIdAsync(id);
        if (deckCard == null)
        {
            logger.LogOperationWarning(UpdateDeckCardOperation, "Deck card not found", new { id });
            return null;
        }

        var previousMaindeck = deckCard.MaindeckQuantity;
        var previousSideboard = deckCard.SideboardQuantity;
        var requestedQuantity = updateDto.MaindeckQuantity + updateDto.SideboardQuantity;
        await EnsureCopyLimitAsync(deckCard.DeckId, deckCard.Name, deckCard.TypeLine, requestedQuantity, deckCard.Id, UpdateDeckCardOperation);

        deckCard.MaindeckQuantity = updateDto.MaindeckQuantity;
        deckCard.SideboardQuantity = updateDto.SideboardQuantity;
        deckCard.OwnedCardId = updateDto.OwnedCardId;

        unitOfWork.Repository<DeckCard>().Update(deckCard);
        await unitOfWork.Complete();

        // Reload the entity to get the updated values
        var updatedDeckCard = await unitOfWork.Repository<DeckCard>().GetByIdAsync(id, tracking: false);
        if (updatedDeckCard == null)
        {
            logger.LogOperationWarning(UpdateDeckCardOperation, "Deck card missing after update", new { id });
            return null;
        }
        
        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(updatedDeckCard.DeckId);
        if (deck == null)
        {
            logger.LogOperationWarning(UpdateDeckCardOperation, "Deck missing after deck card update", new { updatedDeckCard.DeckId });
            return null;
        }
        
        var marketProvider = await ResolveMarketProviderAsync(deck.OwnerId);
        await UpdateDeckAggregatesAsync(deck, updatedDeckCard, marketProvider, previousMaindeck, previousSideboard);
        await UpdateDeckColorIdentityAsync(deck, updatedDeckCard.ColorIdentity);
        var dto = await MapToDtoAsync(updatedDeckCard, deck.OwnerId, marketProvider);
        logger.LogOperationSuccess(UpdateDeckCardOperation, new { id, updatedDeckCard.DeckId });
        return dto;
    }
    
    public async Task<DeckCardDto?> UpdateDeckCardVersionAsync(int id, UpdateDeckCardVersionDto updateDto, ScryfallCardDto scryfallCard)
    {
        using var scope = logger.BeginOperationScope(UpdateDeckCardOperation, id);
        logger.LogOperationStart(UpdateDeckCardOperation, new { id });

        var imageUris = CardDataService.ResolveImageUris(scryfallCard);
        var resolvedImage = imageUris?.Normal ?? imageUris?.Large ?? imageUris?.Png;
        var resolvedBackImage = cardDataService.ResolveBackImageUrl(scryfallCard);
        
        var deckCard = await unitOfWork.Repository<DeckCard>().GetByIdAsync(id);
        if (deckCard == null)
        {
            logger.LogOperationWarning(UpdateDeckCardOperation, "Deck card not found", new { id });
            return null;
        }

        var requestedQuantity = updateDto.MaindeckQuantity + updateDto.SideboardQuantity;
        var resolvedTypeLine = scryfallCard.TypeLine ?? deckCard.TypeLine;
        await EnsureCopyLimitAsync(deckCard.DeckId, deckCard.Name, resolvedTypeLine, requestedQuantity, deckCard.Id, UpdateDeckCardOperation);

        var previousMaindeck = deckCard.MaindeckQuantity;
        var previousSideboard = deckCard.SideboardQuantity;
        deckCard.MaindeckQuantity = updateDto.MaindeckQuantity;
        deckCard.SideboardQuantity = updateDto.SideboardQuantity;
        deckCard.OwnedCardId = updateDto.OwnedCardId;
        deckCard.ScryfallId = scryfallCard.Id;
        deckCard.SetName = scryfallCard.SetName;
        deckCard.SetCode = scryfallCard.SetId!;
        deckCard.ArtCrop = imageUris!.ArtCrop!;
        deckCard.ImageUrl = resolvedImage!;
        deckCard.BackImageUrl = resolvedBackImage;
        deckCard.CollectorNumber = scryfallCard.CollectorNumber;
        deckCard.Rarity = scryfallCard.Rarity;
        deckCard.TypeLine = resolvedTypeLine ?? deckCard.TypeLine;
        deckCard.ColorIdentity = NormalizeColorIdentity(scryfallCard.ColorIdentity);

        unitOfWork.Repository<DeckCard>().Update(deckCard);
        await unitOfWork.Complete();

        // Reload the entity to get the updated values
        var updatedDeckCard = await unitOfWork.Repository<DeckCard>().GetByIdAsync(id, tracking: false);
        if (updatedDeckCard == null)
        {
            logger.LogOperationWarning(UpdateDeckCardOperation, "Deck card missing after update", new { id });
            return null;
        }
        
        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(updatedDeckCard.DeckId);
        if (deck == null)
        {
            logger.LogOperationWarning(UpdateDeckCardOperation, "Deck missing after deck card update", new { updatedDeckCard.DeckId });
            return null;
        }
        
        var marketProvider = await ResolveMarketProviderAsync(deck.OwnerId);
        await UpdateDeckAggregatesAsync(deck, updatedDeckCard, marketProvider, previousMaindeck, previousSideboard);
        await UpdateDeckColorIdentityAsync(deck, updatedDeckCard.ColorIdentity);
        var dto = await MapToDtoAsync(updatedDeckCard, deck.OwnerId, marketProvider);
        logger.LogOperationSuccess(UpdateDeckCardOperation, new { id, updatedDeckCard.DeckId });
        return dto;
    }

    public async Task<bool> DeleteDeckCardAsync(int id)
    {
        using var scope = logger.BeginOperationScope(DeleteDeckCardOperation, id);
        logger.LogOperationStart(DeleteDeckCardOperation, new { id });

        var deckCard = await unitOfWork.Repository<DeckCard>().GetByIdAsync(id);
        if (deckCard == null)
        {
            logger.LogOperationWarning(DeleteDeckCardOperation, "Deck card not found", new { id });
            return false;
        }

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckCard.DeckId);
        if (deck == null)
        {
            logger.LogOperationWarning(DeleteDeckCardOperation, "Deck missing for deck card deletion", new { deckCard.DeckId, id });
            return false;
        }

        var previousMaindeck = deckCard.MaindeckQuantity;
        var previousSideboard = deckCard.SideboardQuantity;
        deckCard.MaindeckQuantity = 0;
        deckCard.SideboardQuantity = 0;
        var marketProvider = await ResolveMarketProviderAsync(deck.OwnerId);
        await UpdateDeckAggregatesAsync(deck, deckCard, marketProvider, previousMaindeck, previousSideboard);

        unitOfWork.Repository<DeckCard>().Delete(deckCard);
        await unitOfWork.Complete();

        logger.LogOperationSuccess(DeleteDeckCardOperation, new { id, deckCard.DeckId });
        return true;
    }

    private DeckCardDto MapToDto(DeckCard deckCard, MarketProvider marketProvider)
    {
        var price = ResolveMarketPrice(deckCard.ScryfallId, marketProvider);
        return new DeckCardDto
        {
            Id = deckCard.Id,
            DeckId = deckCard.DeckId,
            ScryfallId = deckCard.ScryfallId,
            Name = deckCard.Name,
            SetCode = deckCard.SetCode,
            SetName = deckCard.SetName,
            ColorIdentity = deckCard.ColorIdentity,
            ImageUrl = deckCard.ImageUrl,
            BackImageUrl = deckCard.BackImageUrl,
            ArtCrop = deckCard.ArtCrop,
            Rarity = deckCard.Rarity,
            CollectorNumber = deckCard.CollectorNumber,
            TypeLine = deckCard.TypeLine,
            MaindeckQuantity = deckCard.MaindeckQuantity,
            SideboardQuantity = deckCard.SideboardQuantity,
            OwnedCardId = deckCard.OwnedCardId,
            OwnedQuantity = deckCard.OwnedCard?.Quantity ?? 0,
            Price = price,
            PriceCurrency = price.HasValue ? marketProvider : null
        };
    }

    private async Task<DeckCardDto> MapToDtoAsync(DeckCard deckCard, string ownerId, MarketProvider marketProvider)
    {
        // Find all collections owned by the user
        var userCollectionsSpec = new CollectionWithOwnerSpecification(ownerId);
        var userCollections = await unitOfWork.Repository<Collection>().ListAsync(userCollectionsSpec, tracking: false);
        
        // Find all cards in those collections that match the ScryfallId
        var totalOwnedQuantity = 0;
        Card? firstOwnedCard = null;
        
        if (userCollections != null)
        {
            foreach (var collection in userCollections)
            {
                var cardsSpec = new CardsWithParamsSpecification(new EntitySpecParams(), collection.Id);
                var collectionCards = await unitOfWork.Repository<Card>().ListAsync(cardsSpec, tracking: false);
                
                var matchingCards = collectionCards?.Where(c => c.ScryfallId == deckCard.ScryfallId).ToList();
                if (matchingCards?.Any() == true)
                {
                    totalOwnedQuantity += matchingCards.Sum(c => c.Quantity);
                    firstOwnedCard ??= matchingCards.First();
                }
            }
        }
        
        var price = ResolveMarketPrice(deckCard.ScryfallId, marketProvider);
        return new DeckCardDto
        {
            Id = deckCard.Id,
            DeckId = deckCard.DeckId,
            ScryfallId = deckCard.ScryfallId,
            Name = deckCard.Name,
            SetCode = deckCard.SetCode,
            SetName = deckCard.SetName,
            ColorIdentity = deckCard.ColorIdentity,
            ImageUrl = deckCard.ImageUrl,
            BackImageUrl = deckCard.BackImageUrl,
            ArtCrop = deckCard.ArtCrop,
            Rarity = deckCard.Rarity,
            CollectorNumber = deckCard.CollectorNumber,
            TypeLine = deckCard.TypeLine,
            MaindeckQuantity = deckCard.MaindeckQuantity,
            SideboardQuantity = deckCard.SideboardQuantity,
            OwnedCardId = firstOwnedCard?.Id ?? deckCard.OwnedCardId,
            OwnedQuantity = totalOwnedQuantity,
            Price = price,
            PriceCurrency = price.HasValue ? marketProvider : null
        };
    }

    private DeckCardDto MapToDtoWithLookup(DeckCard deckCard, Dictionary<string, List<Card>> ownedCardsLookup, MarketProvider marketProvider)
    {
        var ownedCards = ownedCardsLookup.TryGetValue(deckCard.ScryfallId, out var cards) ? cards : new List<Card>();
        var totalOwnedQuantity = ownedCards.Sum(c => c.Quantity);
        var firstOwnedCard = ownedCards.FirstOrDefault();
        var price = ResolveMarketPrice(deckCard.ScryfallId, marketProvider);
        
        return new DeckCardDto
        {
            Id = deckCard.Id,
            DeckId = deckCard.DeckId,
            ScryfallId = deckCard.ScryfallId,
            Name = deckCard.Name,
            SetCode = deckCard.SetCode,
            SetName = deckCard.SetName,
            ImageUrl = deckCard.ImageUrl,
            BackImageUrl = deckCard.BackImageUrl,
            ArtCrop = deckCard.ArtCrop,
            Rarity = deckCard.Rarity,
            CollectorNumber = deckCard.CollectorNumber,
            TypeLine = deckCard.TypeLine,
            MaindeckQuantity = deckCard.MaindeckQuantity,
            SideboardQuantity = deckCard.SideboardQuantity,
            OwnedCardId = firstOwnedCard?.Id ?? deckCard.OwnedCardId,
            OwnedQuantity = totalOwnedQuantity,
            Price = price,
            PriceCurrency = price.HasValue ? marketProvider : null
        };
    }

    private async Task UpdateDeckAggregatesAsync(
        Deck deck,
        DeckCard deckCard,
        MarketProvider marketProvider,
        int previousMaindeck,
        int previousSideboard)
    {
        var mainDelta = deckCard.MaindeckQuantity - previousMaindeck;
        var sideDelta = deckCard.SideboardQuantity - previousSideboard;

        if (mainDelta != 0)
        {
            deck.NumberOfMainBoardCards = Math.Max(0, deck.NumberOfMainBoardCards + mainDelta);
        }

        if (sideDelta != 0)
        {
            deck.NumberOfSideBoardCards = Math.Max(0, deck.NumberOfSideBoardCards + sideDelta);
        }

        var totalDelta = mainDelta + sideDelta;
        if (totalDelta != 0)
        {
            deck.NumberOfCards = Math.Max(0, deck.NumberOfCards + totalDelta);
        }

        var price = ResolveMarketPrice(deckCard.ScryfallId, marketProvider);
        if (price.HasValue && totalDelta != 0)
        {
            deck.TotalPriceCurrency = ResolveCurrency(marketProvider);
            var updatedTotal = deck.TotalPrice + (price.Value * totalDelta);
            deck.TotalPrice = Math.Round(Math.Max(0, updatedTotal), 2, MidpointRounding.AwayFromZero);
        }

        if (mainDelta != 0 || sideDelta != 0 || (price.HasValue && totalDelta != 0))
        {
            unitOfWork.Repository<Deck>().Update(deck);
            await unitOfWork.Complete();
        }
    }

    private async Task UpdateDeckColorIdentityAsync(Deck deck, IEnumerable<string>? colorIdentity)
    {
        if (deck == null || colorIdentity == null)
        {
            return;
        }

        deck.ColorIdentity ??= new List<string>();
        var added = false;
        foreach (var color in colorIdentity.Where(ci => !string.IsNullOrWhiteSpace(ci)))
        {
            if (!deck.ColorIdentity.Any(existing => string.Equals(existing, color, StringComparison.OrdinalIgnoreCase)))
            {
                deck.ColorIdentity.Add(color);
                added = true;
            }
        }

        if (added)
        {
            unitOfWork.Repository<Deck>().Update(deck);
            await unitOfWork.Complete();
        }
    }

    private async Task EnsureCopyLimitAsync(int deckId, string cardName, string? typeLine, int requestedTotalQuantity, int? existingDeckCardId, string operation)
    {
        if (IsBasicLand(typeLine))
        {
            return;
        }

        var spec = new DeckCardsWithDeckIdSpecification(deckId);
        var deckCards = await unitOfWork.Repository<DeckCard>().ListAsync(spec, tracking: false) ?? [];

        var existingTotal = deckCards
            .Where(dc => string.Equals(dc.Name, cardName, StringComparison.OrdinalIgnoreCase))
            .Where(dc => !existingDeckCardId.HasValue || dc.Id != existingDeckCardId.Value)
            .Sum(dc => dc.MaindeckQuantity + dc.SideboardQuantity);

        if (existingTotal + requestedTotalQuantity > 4)
        {
            logger.LogOperationWarning(operation, "Copy limit exceeded", new { deckId, cardName, requestedTotalQuantity });
            throw new InvalidOperationException($"Adding '{cardName}' would exceed the 4-copy limit for this deck.");
        }
    }

    private static bool IsBasicLand(string? typeLine)
    {
        return !string.IsNullOrWhiteSpace(typeLine) && typeLine.IndexOf("Basic", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static List<string> NormalizeColorIdentity(IEnumerable<string?>? colorIdentity)
    {
        return (colorIdentity ?? Enumerable.Empty<string?>())
            .Where(ci => !string.IsNullOrWhiteSpace(ci))
            .Select(ci => ci!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Currency ResolveCurrency(MarketProvider marketProvider)
    {
        return marketProvider == MarketProvider.Mkm ? Currency.Eur : Currency.Usd;
    }

    private async Task<MarketProvider> ResolveMarketProviderAsync(string ownerId)
    {
        var settings = await identityDbContext.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.AppUserId == ownerId);
        return settings?.MarketProvider ?? MarketProvider.Mkm;
    }

    private async Task<MarketProvider> ResolveMarketProviderForDeckAsync(int deckId)
    {
        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId, tracking: false);
        return deck is null ? MarketProvider.Mkm : await ResolveMarketProviderAsync(deck.OwnerId);
    }

    private double? ResolveMarketPrice(string scryfallId, MarketProvider marketProvider)
    {
        if (!cardDataService.CardDataById.TryGetValue(scryfallId, out var marketData) || marketData?.Prices is null)
        {
            return null;
        }

        var priceText = marketProvider == MarketProvider.Mkm
            ? marketData.Prices.Eur
            : marketData.Prices.Usd;

        if (string.IsNullOrWhiteSpace(priceText))
        {
            return null;
        }

        return double.TryParse(priceText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
