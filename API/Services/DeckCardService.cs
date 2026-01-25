using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using API.Dtos.Cards;
using API.Dtos.Decks;
using API.Logging;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Core.Specifications;
using Microsoft.Extensions.Logging;

namespace API.Services;

public class DeckCardService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ILogger<DeckCardService> logger;
    private readonly CardDataService cardDataService;
    private readonly IUserSettingsService userSettingsService;

    private const string GetDeckCardsOperation = "DeckCards.Fetch";
    private const string CreateDeckCardOperation = "DeckCards.Create";
    private const string UpdateDeckCardOperation = "DeckCards.Update";
    private const string DeleteDeckCardOperation = "DeckCards.Delete";

    private static readonly HashSet<string> BasicLandNames = new HashSet<string>(new[]
    {
        "Island",
        "Forest",
        "Mountain",
        "Swamp",
        "Plains"
    }, StringComparer.OrdinalIgnoreCase);

    public DeckCardService(IUnitOfWork unitOfWork, ILogger<DeckCardService> logger, CardDataService cardDataService, IUserSettingsService userSettingsService)
    {
        this.unitOfWork = unitOfWork;
        this.logger = logger;
        this.cardDataService = cardDataService;
        this.userSettingsService = userSettingsService;
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

        // Build a lookup of deck card names so ownership can be matched across printings
        var cardNames = (deckCards ?? [])
            .Select(dc => dc.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name) && !IsBasicLandName(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var cardNameSet = new HashSet<string>(cardNames, StringComparer.OrdinalIgnoreCase);
        
        // Get user's collections
        var userCollectionsSpec = new CollectionWithOwnerSpecification(deck.OwnerId);
        var userCollections = await unitOfWork.Repository<Collection>().ListAsync(userCollectionsSpec, tracking: false);
        
        // Get all owned cards that match the deck card names in one query per collection
        var ownedCardsLookup = new Dictionary<string, List<Card>>(StringComparer.OrdinalIgnoreCase);
        
        if (userCollections != null && cardNameSet.Count > 0)
        {
            foreach (var collection in userCollections)
            {
                var cardsSpec = new CardsWithParamsSpecification(
                    new EntitySpecParams(),
                    collection.Id,
                    applySorting: false,
                    applyPaging: false);
                var collectionCards = await unitOfWork.Repository<Card>().ListAsync(cardsSpec, tracking: false);
                
                var matchingCards = collectionCards?
                    .Where(c => !string.IsNullOrWhiteSpace(c.Name) && cardNameSet.Contains(c.Name))
                    .ToList();
                if (matchingCards?.Any() == true)
                {
                    foreach (var card in matchingCards)
                    {
                        var lookupKey = card.Name;
                        if (string.IsNullOrWhiteSpace(lookupKey))
                        {
                            continue;
                        }

                        if (!ownedCardsLookup.TryGetValue(lookupKey, out var cards))
                        {
                            cards = new List<Card>();
                            ownedCardsLookup[lookupKey] = cards;
                        }
                        cards.Add(card);
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
        logger.LogOperationStart(CreateDeckCardOperation, new { deckId, createDto.Name });

        if (string.IsNullOrWhiteSpace(createDto.Name))
        {
            logger.LogOperationWarning(CreateDeckCardOperation, "Missing card name", new { deckId });
            throw new ArgumentException("Card name is required.");
        }

        var requestedQuantity = createDto.MaindeckQuantity + createDto.SideboardQuantity;
        if (requestedQuantity <= 0)
        {
            logger.LogOperationWarning(CreateDeckCardOperation, "Invalid quantity", new { deckId, createDto.Name });
            throw new InvalidOperationException("You must add at least one copy of the card.");
        }

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null)
        {
            logger.LogOperationWarning(CreateDeckCardOperation, "Deck not found after card creation", new { deckId });
            throw new InvalidOperationException("Deck not found");
        }

        var trimmedName = createDto.Name.Trim();
        var ownedCard = await FindOwnedCardByNameAsync(deck.OwnerId, trimmedName);

        ScryfallCardDto? scryfallCard = null;
        string resolvedName;
        string resolvedScryfallId;
        string setCode;
        string? setName;
        string typeLine;
        string imageUrl;
        string? backImageUrl;
        string artCrop;
        string? rarity;
        string? collectorNumber;
        int? ownedCardId = null;

        if (ownedCard != null)
        {
            resolvedName = ownedCard.Name;
            resolvedScryfallId = ownedCard.ScryfallId;
            setCode = ownedCard.SetCode;
            setName = ownedCard.SetName;
            typeLine = ownedCard.TypeLine;
            imageUrl = ownedCard.ImageUrl;
            backImageUrl = ownedCard.BackImageUrl;
            artCrop = ownedCard.ArtCrop;
            rarity = ownedCard.Rarity;
            collectorNumber = ownedCard.CollectorNumber;
            ownedCardId = ownedCard.Id;

            if (!cardDataService.CardDataById.TryGetValue(ownedCard.ScryfallId, out scryfallCard))
            {
                cardDataService.CardDataByName.TryGetValue(ownedCard.Name, out scryfallCard);
            }
        }
        else
        {
            if (!cardDataService.CardDataByName.TryGetValue(trimmedName, out scryfallCard))
            {
                logger.LogOperationWarning(CreateDeckCardOperation, "Card not found in Scryfall data", new { deckId, Name = trimmedName });
                throw new InvalidOperationException($"Card '{trimmedName}' was not found in the card database.");
            }

            var imageUris = CardDataService.ResolveImageUris(scryfallCard);
            var resolvedImage = imageUris?.Normal ?? imageUris?.Large ?? imageUris?.Png;
            if (string.IsNullOrWhiteSpace(resolvedImage) || string.IsNullOrWhiteSpace(imageUris?.ArtCrop))
            {
                logger.LogOperationWarning(CreateDeckCardOperation, "Card missing imagery", new { deckId, Name = trimmedName });
                throw new InvalidOperationException($"Card '{trimmedName}' is missing image data.");
            }

            resolvedName = scryfallCard.Name;
            resolvedScryfallId = scryfallCard.Id;
            setCode = scryfallCard.Set;
            setName = scryfallCard.SetName;
            typeLine = scryfallCard.TypeLine ?? string.Empty;
            imageUrl = resolvedImage;
            backImageUrl = cardDataService.ResolveBackImageUrl(scryfallCard);
            artCrop = imageUris.ArtCrop!;
            rarity = scryfallCard.Rarity;
            collectorNumber = scryfallCard.CollectorNumber;
        }

        var effectiveTypeLine = string.IsNullOrWhiteSpace(typeLine)
            ? scryfallCard?.TypeLine ?? string.Empty
            : typeLine;
        await EnsureCopyLimitAsync(deckId, resolvedName, effectiveTypeLine, requestedQuantity, null, CreateDeckCardOperation);

        var colorIdentity = NormalizeColorIdentity(scryfallCard?.ColorIdentity);

        var deckCard = new DeckCard
        {
            DeckId = deckId,
            ScryfallId = resolvedScryfallId,
            Name = resolvedName,
            SetCode = setCode,
            SetName = setName,
            TypeLine = string.IsNullOrWhiteSpace(effectiveTypeLine) ? "Card" : effectiveTypeLine,
            ColorIdentity = colorIdentity,
            ImageUrl = imageUrl,
            BackImageUrl = backImageUrl,
            ArtCrop = artCrop,
            Rarity = rarity,
            CollectorNumber = collectorNumber,
            MaindeckQuantity = createDto.MaindeckQuantity,
            SideboardQuantity = createDto.SideboardQuantity,
            OwnedCardId = ownedCardId
        };

        unitOfWork.Repository<DeckCard>().Add(deckCard);
        await unitOfWork.Complete();

        var marketProvider = await ResolveMarketProviderAsync(deck.OwnerId);
        await UpdateDeckAggregatesAsync(deck, deckCard, marketProvider, previousMaindeck: 0, previousSideboard: 0);
        await UpdateDeckColorIdentityAsync(deck, deckCard.ColorIdentity);
        await RecalculateDeckColorIdentityAsync(deck.Id);
        await UpdateDeckListAsync(deck);
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
        await RecalculateDeckColorIdentityAsync(deck.Id);
        if (previousMaindeck != updatedDeckCard.MaindeckQuantity || previousSideboard != updatedDeckCard.SideboardQuantity)
        {
            await UpdateDeckListAsync(deck);
        }
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
        await RecalculateDeckColorIdentityAsync(deck.Id);
        if (previousMaindeck != updatedDeckCard.MaindeckQuantity || previousSideboard != updatedDeckCard.SideboardQuantity)
        {
            await UpdateDeckListAsync(deck);
        }
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
        await RecalculateDeckColorIdentityAsync(deck.Id);
        await UpdateDeckListAsync(deck);

        logger.LogOperationSuccess(DeleteDeckCardOperation, new { id, deckCard.DeckId });
        return true;
    }

    private DeckCardDto MapToDto(DeckCard deckCard, MarketProvider marketProvider)
    {
        if (IsBasicLandName(deckCard.Name))
        {
            return CreateDeckCardDto(deckCard, marketProvider, ResolveBasicLandOwnedQuantity(deckCard));
        }

        var ownedQuantity = deckCard.OwnedCard?.Quantity ?? 0;
        return CreateDeckCardDto(deckCard, marketProvider, ownedQuantity);
    }

    private async Task<DeckCardDto> MapToDtoAsync(DeckCard deckCard, string ownerId, MarketProvider marketProvider)
    {
        if (IsBasicLandName(deckCard.Name))
        {
            return CreateDeckCardDto(deckCard, marketProvider, ResolveBasicLandOwnedQuantity(deckCard));
        }

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
                var cardsSpec = new CardsWithParamsSpecification(
                    new EntitySpecParams(),
                    collection.Id,
                    applySorting: false,
                    applyPaging: false);
                var collectionCards = await unitOfWork.Repository<Card>().ListAsync(cardsSpec, tracking: false);
                
                var matchingCards = collectionCards?.Where(c => c.Name == deckCard.Name).ToList();
                if (matchingCards?.Any() == true)
                {
                    totalOwnedQuantity += matchingCards.Sum(c => c.Quantity);
                    firstOwnedCard ??= matchingCards.First();
                }
            }
        }
        
        return CreateDeckCardDto(deckCard, marketProvider, totalOwnedQuantity, firstOwnedCard?.Id);
    }

    private DeckCardDto MapToDtoWithLookup(DeckCard deckCard, Dictionary<string, List<Card>> ownedCardsLookup, MarketProvider marketProvider)
    {
        if (IsBasicLandName(deckCard.Name))
        {
            return CreateDeckCardDto(deckCard, marketProvider, ResolveBasicLandOwnedQuantity(deckCard));
        }

        var lookupKey = deckCard.Name;
        var ownedCards = !string.IsNullOrWhiteSpace(lookupKey) && ownedCardsLookup.TryGetValue(lookupKey, out var cards)
            ? cards
            : new List<Card>();
        var totalOwnedQuantity = ownedCards.Sum(c => c.Quantity);
        var firstOwnedCard = ownedCards.FirstOrDefault();
        
        return CreateDeckCardDto(deckCard, marketProvider, totalOwnedQuantity, firstOwnedCard?.Id);
    }

    private DeckCardDto CreateDeckCardDto(DeckCard deckCard, MarketProvider marketProvider, int ownedQuantity, int? ownedCardIdOverride = null)
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
            OwnedCardId = ownedCardIdOverride ?? deckCard.OwnedCardId,
            OwnedQuantity = ownedQuantity,
            Price = price,
            PriceCurrency = price.HasValue ? marketProvider : null
        };
    }

    private static bool IsBasicLandName(string? cardName)
    {
        return !string.IsNullOrWhiteSpace(cardName) && BasicLandNames.Contains(cardName.Trim());
    }

    private static bool IsBasicLandTypeLine(string? typeLine)
    {
        return !string.IsNullOrWhiteSpace(typeLine) &&
            typeLine.Contains("Basic Land", StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveBasicLandOwnedQuantity(DeckCard deckCard)
    {
        var total = GetTotalQuantity(deckCard);
        return Math.Max(total, 4);
    }

    private static int GetTotalQuantity(DeckCard deckCard)
    {
        return deckCard.MaindeckQuantity + deckCard.SideboardQuantity;
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
            deck.TotalPriceCurrency = userSettingsService.ResolveCurrency(marketProvider);
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

    private async Task UpdateDeckListAsync(Deck deck)
    {
        var deckCards = await unitOfWork.Repository<DeckCard>()
            .ListAsync(new DeckCardsWithDeckIdSpecification(deck.Id), tracking: false);

        var deckList = BuildDeckList(deckCards ?? []);
        if (deck.DeckList == deckList)
        {
            return;
        }

        deck.DeckList = deckList;
        unitOfWork.Repository<Deck>().Update(deck);
        await unitOfWork.Complete();
    }

    private static string BuildDeckList(IEnumerable<DeckCard> deckCards)
    {
        var maindeckLines = deckCards
            .Where(card => card.MaindeckQuantity > 0)
            .OrderBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
            .Select(card => $"{card.MaindeckQuantity} {card.Name}")
            .ToList();

        var sideboardLines = deckCards
            .Where(card => card.SideboardQuantity > 0)
            .OrderBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
            .Select(card => $"{card.SideboardQuantity} {card.Name}")
            .ToList();

        if (maindeckLines.Count == 0 && sideboardLines.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>(maindeckLines);
        if (sideboardLines.Count > 0)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.AddRange(sideboardLines);
        }

        return string.Join('\n', lines);
    }

    private async Task<Card?> FindOwnedCardByNameAsync(string ownerId, string cardName)
    {
        if (string.IsNullOrWhiteSpace(cardName))
        {
            return null;
        }

        var userCollectionsSpec = new CollectionWithOwnerSpecification(ownerId);
        var collections = await unitOfWork.Repository<Collection>().ListAsync(userCollectionsSpec, tracking: false);
        if (collections == null)
        {
            return null;
        }

        foreach (var collection in collections)
        {
            var cardsSpec = new CardsWithParamsSpecification(
                new EntitySpecParams { Search = cardName },
                collection.Id,
                applySorting: false,
                applyPaging: false);
            var collectionCards = await unitOfWork.Repository<Card>().ListAsync(cardsSpec, tracking: false);
            var ownedCard = collectionCards?.FirstOrDefault(c => string.Equals(c.Name, cardName, StringComparison.OrdinalIgnoreCase));
            if (ownedCard != null)
            {
                return ownedCard;
            }
        }

        return null;
    }

    private async Task RecalculateDeckColorIdentityAsync(int deckId)
    {
        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null)
        {
            return;
        }

        var spec = new DeckCardsWithDeckIdSpecification(deckId);
        var deckCards = await unitOfWork.Repository<DeckCard>().ListAsync(spec, tracking: false) ?? [];

        var colors = deckCards
            .Where(dc => dc.ColorIdentity != null)
            .SelectMany(dc => dc.ColorIdentity)
            .Where(ci => !string.IsNullOrWhiteSpace(ci))
            .Select(ci => ci!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        deck.ColorIdentity = colors;
        unitOfWork.Repository<Deck>().Update(deck);
        await unitOfWork.Complete();
    }

    private async Task EnsureCopyLimitAsync(int deckId, string cardName, string? typeLine, int requestedTotalQuantity, int? existingDeckCardId, string operation)
    {
        if (IsBasicLandTypeLine(typeLine))
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

    private static List<string> NormalizeColorIdentity(IEnumerable<string?>? colorIdentity)
    {
        return (colorIdentity ?? Enumerable.Empty<string?>())
            .Where(ci => !string.IsNullOrWhiteSpace(ci))
            .Select(ci => ci!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private Task<MarketProvider> ResolveMarketProviderAsync(string ownerId)
    {
        return userSettingsService.GetMarketProviderAsync(ownerId);
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
