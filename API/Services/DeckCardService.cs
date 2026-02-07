using System.Globalization;
using API.Dtos.Cards;
using API.Dtos.Decks;
using API.Helpers;
using API.Logging;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Core.Specifications;

namespace API.Services;

public class DeckCardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeckCardService> _logger;
    private readonly CardDataService _cardDataService;
    private readonly IUserSettingsService _userSettingsService;

    private const string GetDeckCardsOperation = "DeckCards.Fetch";
    private const string CreateDeckCardOperation = "DeckCards.Create";
    private const string ImportDeckCardsOperation = "DeckCards.ImportCreate";
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
        _unitOfWork = unitOfWork;
        _logger = logger;
        _cardDataService = cardDataService;
        _userSettingsService = userSettingsService;
    }

    public async Task<IEnumerable<DeckCardDto>> GetDeckCardsAsync(int deckId, bool maindeckOnly = false, bool sideboardOnly = false, bool? ownedOnly = null)
    {
        using var scope = _logger.BeginOperationScope(GetDeckCardsOperation, deckId);
        _logger.LogOperationStart(GetDeckCardsOperation, new { deckId, maindeckOnly, sideboardOnly, ownedOnly });

        ISpecification<DeckCard> spec = (maindeckOnly, sideboardOnly) switch
        {
            (true, false) => new DeckCardsWithDeckIdSpecification(deckId, maindeckOnly: true),
            (false, true) => new DeckCardsWithDeckIdSpecification(deckId, maindeckOnly: false, sideboardOnly: true),
            (true, true) => new DeckCardsWithDeckIdSpecification(deckId, maindeckOnly: true, sideboardOnly: true),
            _ => new DeckCardsWithDeckIdSpecification(deckId)
        };
        
        var deckCards = await _unitOfWork.Repository<DeckCard>().ListAsync(spec, tracking: false);

        // Get the deck owner for collection lookup
        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(deckId, tracking: false);
        if (deck == null)
        {
            _logger.LogOperationWarning(GetDeckCardsOperation, "Deck not found", new { deckId });
            return [];
        }

        var marketProvider = await ResolveMarketProviderAsync(deck.OwnerId);

        // Build a lookup of deck card Oracle IDs so ownership can be matched across printings
        var cardOracleIds = (deckCards ?? [])
            .Where(dc => !IsBasicLandName(dc.Name))
            .Select(dc => dc.OracleId)
            .Where(oracleId => !string.IsNullOrWhiteSpace(oracleId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var cardOracleIdSet = new HashSet<string>(cardOracleIds, StringComparer.OrdinalIgnoreCase);
        
        // Get user's collections
        var userCollectionsSpec = new CollectionWithOwnerSpecification(deck.OwnerId);
        var userCollections = await _unitOfWork.Repository<Collection>().ListAsync(userCollectionsSpec, tracking: false);
        
        // Get all owned cards that match the deck card names in one query per collection
        var ownedCardsLookup = new Dictionary<string, List<Card>>(StringComparer.OrdinalIgnoreCase);

        if (userCollections != null && cardOracleIdSet.Count > 0)
        {
            foreach (var collection in userCollections)
            {
                var cardsSpec = new CardsWithParamsSpecification(
                    new EntitySpecParams(),
                    collection.Id,
                    applySorting: false,
                    applyPaging: false);
                var collectionCards = await _unitOfWork.Repository<Card>().ListAsync(cardsSpec, tracking: false);
                
                var matchingCards = collectionCards?
                    .Where(c => !string.IsNullOrWhiteSpace(c.OracleId) && cardOracleIdSet.Contains(c.OracleId))
                    .ToList();
                if (matchingCards?.Any() == true)
                {
                    foreach (var card in matchingCards)
                    {
                        var lookupKey = card.OracleId;
                        if (string.IsNullOrWhiteSpace(lookupKey))
                        {
                            continue;
                        }

                        if (!ownedCardsLookup.TryGetValue(lookupKey, out var cards))
                        {
                            cards = [];
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
        
        _logger.LogOperationSuccess(GetDeckCardsOperation, new { deckId, result.Count, ownedOnly });
        return result;
    }

    public async Task<IEnumerable<DeckCardDto>> GetDeckCardsByScryfallIdAsync(string scryfallId, int? deckId = null, string? userId = null)
    {
        const string operation = "DeckCards.FetchByScryfallId";
        var scopeKey = deckId?.ToString() ?? userId ?? scryfallId;
        using var scope = _logger.BeginOperationScope(operation, scopeKey);
        _logger.LogOperationStart(operation, new { scryfallId = scryfallId, deckId, userId });

        ISpecification<DeckCard> spec = deckId.HasValue
            ? new DeckCardsWithScryfallIdSpecification(scryfallId, deckId.Value)
            : new DeckCardsWithScryfallIdSpecification(scryfallId, userId!);
            
        var deckCards = await _unitOfWork.Repository<DeckCard>().ListAsync(spec, tracking: false);

        var marketProvider = deckId.HasValue
            ? await ResolveMarketProviderForDeckAsync(deckId.Value)
            : (!string.IsNullOrEmpty(userId) ? await ResolveMarketProviderAsync(userId) : MarketProvider.Mkm);

        var mapped = deckCards?.Select(dc => MapToDto(dc, marketProvider)).ToList() ?? [];
        _logger.LogOperationSuccess(operation, new { scryfallId, mapped.Count });
        return mapped;
    }

    public async Task<DeckCardDto?> GetDeckCardByIdAsync(int id)
    {
        const string operation = "DeckCards.FetchById";
        using var scope = _logger.BeginOperationScope(operation, id);
        _logger.LogOperationStart(operation, new { id });

        var deckCard = await _unitOfWork.Repository<DeckCard>().GetByIdAsync(id, tracking: false);
        if (deckCard == null)
        {
            _logger.LogOperationWarning(operation, "Deck card not found", new { id });
            return null;
        }
        
        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(deckCard.DeckId, tracking: false);
        if (deck == null)
        {
            _logger.LogOperationWarning(operation, "Deck missing for deck card", new { deckCard.DeckId, deckCard.Id });
            return null;
        }
        
        var marketProvider = await ResolveMarketProviderAsync(deck.OwnerId);
        var dto = await MapToDtoAsync(deckCard, deck.OwnerId, marketProvider);
        _logger.LogOperationSuccess(operation, new { id, deckCard.DeckId });
        return dto;
    }

    public async Task<DeckCardDto> CreateDeckCardAsync(int deckId, CreateDeckCardDto createDto)
    {
        using var scope = _logger.BeginOperationScope(CreateDeckCardOperation, deckId);
        _logger.LogOperationStart(CreateDeckCardOperation, new { deckId, createDto.Name });

        if (string.IsNullOrWhiteSpace(createDto.Name))
        {
            _logger.LogOperationWarning(CreateDeckCardOperation, "Missing card name", new { deckId });
            throw new ArgumentException("Card name is required.");
        }

        var requestedQuantity = createDto.MaindeckQuantity + createDto.SideboardQuantity;
        if (requestedQuantity <= 0)
        {
            _logger.LogOperationWarning(CreateDeckCardOperation, "Invalid quantity", new { deckId, createDto.Name });
            throw new InvalidOperationException("You must add at least one copy of the card.");
        }

        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null)
        {
            _logger.LogOperationWarning(CreateDeckCardOperation, "Deck not found after card creation", new { deckId });
            throw new InvalidOperationException("Deck not found");
        }

        var trimmedName = createDto.Name.Trim();
        if (!_cardDataService.CardDataByName.TryGetValue(trimmedName, out var namedCardData))
        {
            _logger.LogOperationWarning(CreateDeckCardOperation, "Card not found in Scryfall data", new { deckId, Name = trimmedName });
            throw new InvalidOperationException($"Card '{trimmedName}' was not found in the card database.");
        }

        var oracleId = namedCardData.OracleId;
        var ownedCard = await FindOwnedCardByOracleIdAsync(deck.OwnerId, oracleId);

        ScryfallCardDto? scryfallCard = namedCardData;
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
            oracleId = ownedCard.OracleId;
            setCode = ownedCard.SetCode;
            setName = ownedCard.SetName;
            typeLine = ownedCard.TypeLine;
            imageUrl = ownedCard.ImageUrl;
            backImageUrl = ownedCard.BackImageUrl;
            artCrop = ownedCard.ArtCrop;
            rarity = ownedCard.Rarity;
            collectorNumber = ownedCard.CollectorNumber;
            ownedCardId = ownedCard.Id;

            if (!_cardDataService.CardDataById.TryGetValue(ownedCard.ScryfallId, out scryfallCard))
            {
                scryfallCard = namedCardData;
            }
        }
        else
        {
            var imageUris = CardDataService.ResolveImageUris(namedCardData);
            var resolvedImage = imageUris?.Normal ?? imageUris?.Large ?? imageUris?.Png;
            if (string.IsNullOrWhiteSpace(resolvedImage) || string.IsNullOrWhiteSpace(imageUris?.ArtCrop))
            {
                _logger.LogOperationWarning(CreateDeckCardOperation, "Card missing imagery", new { deckId, Name = trimmedName });
                throw new InvalidOperationException($"Card '{trimmedName}' is missing image data.");
            }

            resolvedName = namedCardData.Name;
            resolvedScryfallId = namedCardData.Id;
            oracleId = namedCardData.OracleId;
            setCode = namedCardData.Set;
            setName = namedCardData.SetName;
            typeLine = namedCardData.TypeLine ?? string.Empty;
            imageUrl = resolvedImage;
            backImageUrl = CardDataService.ResolveBackImageUrl(namedCardData);
            artCrop = imageUris.ArtCrop!;
            rarity = namedCardData.Rarity;
            collectorNumber = namedCardData.CollectorNumber;
        }

        if (!DeckLegalityHelper.IsLegal(deck.Format, scryfallCard!))
        {
            _logger.LogOperationWarning(CreateDeckCardOperation, "Card not legal for deck format", new { deckId, deck.Format, Name = trimmedName });
            throw new InvalidOperationException($"Card '{trimmedName}' is not legal in {deck.Format}.");
        }

        var effectiveTypeLine = string.IsNullOrWhiteSpace(typeLine)
            ? scryfallCard?.TypeLine ?? string.Empty
            : typeLine;
        await EnsureCopyLimitAsync(deckId, oracleId, resolvedName, effectiveTypeLine, requestedQuantity, null, CreateDeckCardOperation);

        var colorIdentity = NormalizeColorIdentity(scryfallCard?.ColorIdentity);
        var manaCost = CardDataService.ResolveMainFaceManaCost(scryfallCard);

        var deckCard = new DeckCard
        {
            DeckId = deckId,
            ScryfallId = resolvedScryfallId,
            OracleId = oracleId,
            Name = resolvedName,
            SetCode = setCode,
            SetName = setName,
            TypeLine = string.IsNullOrWhiteSpace(effectiveTypeLine) ? "Card" : effectiveTypeLine,
            ManaCost = manaCost,
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

        _unitOfWork.Repository<DeckCard>().Add(deckCard);
        await _unitOfWork.Complete();

        var marketProvider = await ResolveMarketProviderAsync(deck.OwnerId);
        await UpdateDeckAggregatesAsync(deck, deckCard, marketProvider, previousMaindeck: 0, previousSideboard: 0);
        await UpdateDeckColorIdentityAsync(deck, deckCard.ColorIdentity);
        await RecalculateDeckColorIdentityAsync(deck.Id);
        var dto = await MapToDtoAsync(deckCard, deck.OwnerId, marketProvider);
        _logger.LogOperationSuccess(CreateDeckCardOperation, new { deckCard.Id, deckId });
        return dto;
    }

    public async Task<IReadOnlyList<DeckCardDto>> CreateDeckCardsForImportAsync(
        Deck deck,
        IReadOnlyCollection<CreateDeckCardDto> createDtos,
        MarketProvider marketProvider,
        IReadOnlyDictionary<string, List<Card>> ownedCardsLookup)
    {
        using var scope = _logger.BeginOperationScope(ImportDeckCardsOperation, deck.Id);
        _logger.LogOperationStart(ImportDeckCardsOperation, new { deck.Id, createDtos.Count });

        if (createDtos.Count == 0)
        {
            return Array.Empty<DeckCardDto>();
        }

        var copyCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var deckCards = new List<DeckCard>(createDtos.Count);

        foreach (var createDto in createDtos)
        {
            if (string.IsNullOrWhiteSpace(createDto.Name))
            {
                _logger.LogOperationWarning(ImportDeckCardsOperation, "Missing card name", new { deck.Id });
                throw new ArgumentException("Card name is required.");
            }

            var requestedQuantity = createDto.MaindeckQuantity + createDto.SideboardQuantity;
            if (requestedQuantity <= 0)
            {
                _logger.LogOperationWarning(ImportDeckCardsOperation, "Invalid quantity", new { deck.Id, createDto.Name });
                throw new InvalidOperationException("You must add at least one copy of the card.");
            }

            var trimmedName = createDto.Name.Trim();
            if (!_cardDataService.CardDataByName.TryGetValue(trimmedName, out var namedCardData))
            {
                _logger.LogOperationWarning(ImportDeckCardsOperation, "Card not found in Scryfall data", new { deck.Id, Name = trimmedName });
                throw new InvalidOperationException($"Card '{trimmedName}' was not found in the card database.");
            }

            var oracleId = namedCardData.OracleId;
            ownedCardsLookup.TryGetValue(oracleId, out var ownedCards);
            var ownedCard = ownedCards?.FirstOrDefault();

            ScryfallCardDto? scryfallCard = namedCardData;
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
                oracleId = ownedCard.OracleId;
                setCode = ownedCard.SetCode;
                setName = ownedCard.SetName;
                typeLine = ownedCard.TypeLine;
                imageUrl = ownedCard.ImageUrl;
                backImageUrl = ownedCard.BackImageUrl;
                artCrop = ownedCard.ArtCrop;
                rarity = ownedCard.Rarity;
                collectorNumber = ownedCard.CollectorNumber;
                ownedCardId = ownedCard.Id;

                if (!_cardDataService.CardDataById.TryGetValue(ownedCard.ScryfallId, out scryfallCard))
                {
                    scryfallCard = namedCardData;
                }
            }
            else
            {
                var imageUris = CardDataService.ResolveImageUris(namedCardData);
                var resolvedImage = imageUris?.Normal ?? imageUris?.Large ?? imageUris?.Png;
                if (string.IsNullOrWhiteSpace(resolvedImage) || string.IsNullOrWhiteSpace(imageUris?.ArtCrop))
                {
                    _logger.LogOperationWarning(ImportDeckCardsOperation, "Card missing imagery", new { deck.Id, Name = trimmedName });
                    throw new InvalidOperationException($"Card '{trimmedName}' is missing image data.");
                }

                resolvedName = namedCardData.Name;
                resolvedScryfallId = namedCardData.Id;
                oracleId = namedCardData.OracleId;
                setCode = namedCardData.Set;
                setName = namedCardData.SetName;
                typeLine = namedCardData.TypeLine ?? string.Empty;
                imageUrl = resolvedImage;
                backImageUrl = CardDataService.ResolveBackImageUrl(namedCardData);
                artCrop = imageUris.ArtCrop!;
                rarity = namedCardData.Rarity;
                collectorNumber = namedCardData.CollectorNumber;
            }

            var effectiveTypeLine = string.IsNullOrWhiteSpace(typeLine)
                ? scryfallCard?.TypeLine ?? string.Empty
                : typeLine;

            if (!IsBasicLandTypeLine(effectiveTypeLine))
            {
                copyCounts.TryGetValue(oracleId, out var existingTotal);
                var updatedTotal = existingTotal + requestedQuantity;
                if (updatedTotal > 4)
                {
                    _logger.LogOperationWarning(ImportDeckCardsOperation, "Copy limit exceeded", new { deck.Id, resolvedName, requestedQuantity });
                    throw new InvalidOperationException($"Adding '{resolvedName}' would exceed the 4-copy limit for this deck.");
                }
                copyCounts[oracleId] = updatedTotal;
            }

            var colorIdentity = NormalizeColorIdentity(scryfallCard?.ColorIdentity);
            var manaCost = CardDataService.ResolveMainFaceManaCost(scryfallCard);

            var deckCard = new DeckCard
            {
                DeckId = deck.Id,
                ScryfallId = resolvedScryfallId,
                OracleId = oracleId,
                Name = resolvedName,
                SetCode = setCode,
                SetName = setName,
                TypeLine = string.IsNullOrWhiteSpace(effectiveTypeLine) ? "Card" : effectiveTypeLine,
                ManaCost = manaCost,
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

            _unitOfWork.Repository<DeckCard>().Add(deckCard);
            deckCards.Add(deckCard);
        }

        await _unitOfWork.Complete();

        var result = deckCards.Select(dc => MapToDtoWithLookup(dc, ownedCardsLookup, marketProvider)).ToList();
        _logger.LogOperationSuccess(ImportDeckCardsOperation, new { deck.Id, result.Count });
        return result;
    }

    public async Task<DeckCardDto?> UpdateDeckCardAsync(int id, UpdateDeckCardDto updateDto)
    {
        using var scope = _logger.BeginOperationScope(UpdateDeckCardOperation, id);
        _logger.LogOperationStart(UpdateDeckCardOperation, new { id });

        var deckCard = await _unitOfWork.Repository<DeckCard>().GetByIdAsync(id);
        if (deckCard == null)
        {
            _logger.LogOperationWarning(UpdateDeckCardOperation, "Deck card not found", new { id });
            return null;
        }

        var previousMaindeck = deckCard.MaindeckQuantity;
        var previousSideboard = deckCard.SideboardQuantity;
        var requestedQuantity = updateDto.MaindeckQuantity + updateDto.SideboardQuantity;
        await EnsureCopyLimitAsync(deckCard.DeckId, deckCard.OracleId, deckCard.Name, deckCard.TypeLine, requestedQuantity, deckCard.Id, UpdateDeckCardOperation);

        deckCard.MaindeckQuantity = updateDto.MaindeckQuantity;
        deckCard.SideboardQuantity = updateDto.SideboardQuantity;
        deckCard.OwnedCardId = updateDto.OwnedCardId;

        _unitOfWork.Repository<DeckCard>().Update(deckCard);
        await _unitOfWork.Complete();

        // Reload the entity to get the updated values
        var updatedDeckCard = await _unitOfWork.Repository<DeckCard>().GetByIdAsync(id, tracking: false);
        if (updatedDeckCard == null)
        {
            _logger.LogOperationWarning(UpdateDeckCardOperation, "Deck card missing after update", new { id });
            return null;
        }
        
        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(updatedDeckCard.DeckId);
        if (deck == null)
        {
            _logger.LogOperationWarning(UpdateDeckCardOperation, "Deck missing after deck card update", new { updatedDeckCard.DeckId });
            return null;
        }
        
        var marketProvider = await ResolveMarketProviderAsync(deck.OwnerId);
        await UpdateDeckAggregatesAsync(deck, updatedDeckCard, marketProvider, previousMaindeck, previousSideboard);
        await UpdateDeckColorIdentityAsync(deck, updatedDeckCard.ColorIdentity);
        await RecalculateDeckColorIdentityAsync(deck.Id);
        var dto = await MapToDtoAsync(updatedDeckCard, deck.OwnerId, marketProvider);
        _logger.LogOperationSuccess(UpdateDeckCardOperation, new { id, updatedDeckCard.DeckId });
        return dto;
    }
    
    public async Task<DeckCardDto?> UpdateDeckCardVersionAsync(int id, UpdateDeckCardVersionDto updateDto, ScryfallCardDto scryfallCard)
    {
        using var scope = _logger.BeginOperationScope(UpdateDeckCardOperation, id);
        _logger.LogOperationStart(UpdateDeckCardOperation, new { id });

        var imageUris = CardDataService.ResolveImageUris(scryfallCard);
        var resolvedImage = imageUris?.Normal ?? imageUris?.Large ?? imageUris?.Png;
        var resolvedBackImage = CardDataService.ResolveBackImageUrl(scryfallCard);
        
        var deckCard = await _unitOfWork.Repository<DeckCard>().GetByIdAsync(id);
        if (deckCard == null)
        {
            _logger.LogOperationWarning(UpdateDeckCardOperation, "Deck card not found", new { id });
            return null;
        }

        var requestedQuantity = updateDto.MaindeckQuantity + updateDto.SideboardQuantity;
        var resolvedTypeLine = scryfallCard.TypeLine ?? deckCard.TypeLine;
        await EnsureCopyLimitAsync(deckCard.DeckId, scryfallCard.OracleId, deckCard.Name, resolvedTypeLine, requestedQuantity, deckCard.Id, UpdateDeckCardOperation);

        var previousMaindeck = deckCard.MaindeckQuantity;
        var previousSideboard = deckCard.SideboardQuantity;
        deckCard.MaindeckQuantity = updateDto.MaindeckQuantity;
        deckCard.SideboardQuantity = updateDto.SideboardQuantity;
        deckCard.OwnedCardId = updateDto.OwnedCardId;
        deckCard.ScryfallId = scryfallCard.Id;
        deckCard.OracleId = scryfallCard.OracleId;
        deckCard.SetName = scryfallCard.SetName;
        deckCard.SetCode = scryfallCard.SetId!;
        deckCard.ArtCrop = imageUris!.ArtCrop!;
        deckCard.ImageUrl = resolvedImage!;
        deckCard.BackImageUrl = resolvedBackImage;
        deckCard.CollectorNumber = scryfallCard.CollectorNumber;
        deckCard.Rarity = scryfallCard.Rarity;
        deckCard.TypeLine = resolvedTypeLine ?? deckCard.TypeLine;
        deckCard.ManaCost = CardDataService.ResolveMainFaceManaCost(scryfallCard);
        deckCard.ColorIdentity = NormalizeColorIdentity(scryfallCard.ColorIdentity);

        _unitOfWork.Repository<DeckCard>().Update(deckCard);
        await _unitOfWork.Complete();

        // Reload the entity to get the updated values
        var updatedDeckCard = await _unitOfWork.Repository<DeckCard>().GetByIdAsync(id, tracking: false);
        if (updatedDeckCard == null)
        {
            _logger.LogOperationWarning(UpdateDeckCardOperation, "Deck card missing after update", new { id });
            return null;
        }
        
        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(updatedDeckCard.DeckId);
        if (deck == null)
        {
            _logger.LogOperationWarning(UpdateDeckCardOperation, "Deck missing after deck card update", new { updatedDeckCard.DeckId });
            return null;
        }
        
        var marketProvider = await ResolveMarketProviderAsync(deck.OwnerId);
        await UpdateDeckAggregatesAsync(deck, updatedDeckCard, marketProvider, previousMaindeck, previousSideboard);
        await UpdateDeckColorIdentityAsync(deck, updatedDeckCard.ColorIdentity);
        await RecalculateDeckColorIdentityAsync(deck.Id);
        var dto = await MapToDtoAsync(updatedDeckCard, deck.OwnerId, marketProvider);
        _logger.LogOperationSuccess(UpdateDeckCardOperation, new { id, updatedDeckCard.DeckId });
        return dto;
    }

    public async Task<bool> DeleteDeckCardAsync(int id)
    {
        using var scope = _logger.BeginOperationScope(DeleteDeckCardOperation, id);
        _logger.LogOperationStart(DeleteDeckCardOperation, new { id });

        var deckCard = await _unitOfWork.Repository<DeckCard>().GetByIdAsync(id);
        if (deckCard == null)
        {
            _logger.LogOperationWarning(DeleteDeckCardOperation, "Deck card not found", new { id });
            return false;
        }

        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(deckCard.DeckId);
        if (deck == null)
        {
            _logger.LogOperationWarning(DeleteDeckCardOperation, "Deck missing for deck card deletion", new { deckCard.DeckId, id });
            return false;
        }

        var previousMaindeck = deckCard.MaindeckQuantity;
        var previousSideboard = deckCard.SideboardQuantity;
        deckCard.MaindeckQuantity = 0;
        deckCard.SideboardQuantity = 0;
        var marketProvider = await ResolveMarketProviderAsync(deck.OwnerId);
        await UpdateDeckAggregatesAsync(deck, deckCard, marketProvider, previousMaindeck, previousSideboard);

        _unitOfWork.Repository<DeckCard>().Delete(deckCard);
        await _unitOfWork.Complete();
        await RecalculateDeckColorIdentityAsync(deck.Id);

        _logger.LogOperationSuccess(DeleteDeckCardOperation, new { id, deckCard.DeckId });
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
        var userCollections = await _unitOfWork.Repository<Collection>().ListAsync(userCollectionsSpec, tracking: false);
        
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
                var collectionCards = await _unitOfWork.Repository<Card>().ListAsync(cardsSpec, tracking: false);
                
                var matchingCards = collectionCards?.Where(c => c.OracleId == deckCard.OracleId).ToList();
                if (matchingCards?.Any() == true)
                {
                    totalOwnedQuantity += matchingCards.Sum(c => c.Quantity);
                    firstOwnedCard ??= matchingCards.First();
                }
            }
        }
        
        return CreateDeckCardDto(deckCard, marketProvider, totalOwnedQuantity, firstOwnedCard?.Id);
    }

    private DeckCardDto MapToDtoWithLookup(DeckCard deckCard, IReadOnlyDictionary<string, List<Card>> ownedCardsLookup, MarketProvider marketProvider)
    {
        if (IsBasicLandName(deckCard.Name))
        {
            return CreateDeckCardDto(deckCard, marketProvider, ResolveBasicLandOwnedQuantity(deckCard));
        }

        var lookupKey = deckCard.OracleId;
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
            ManaCost = deckCard.ManaCost,
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
            deck.TotalPriceCurrency = _userSettingsService.ResolveCurrency(marketProvider);
            var updatedTotal = deck.TotalPrice + (price.Value * totalDelta);
            deck.TotalPrice = Math.Round(Math.Max(0, updatedTotal), 2, MidpointRounding.AwayFromZero);
        }

        if (mainDelta != 0 || sideDelta != 0 || (price.HasValue && totalDelta != 0))
        {
            _unitOfWork.Repository<Deck>().Update(deck);
            await _unitOfWork.Complete();
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
            _unitOfWork.Repository<Deck>().Update(deck);
            await _unitOfWork.Complete();
        }
    }

    private async Task<Card?> FindOwnedCardByOracleIdAsync(string ownerId, string oracleId)
    {
        if (string.IsNullOrWhiteSpace(oracleId))
        {
            return null;
        }

        var userCollectionsSpec = new CollectionWithOwnerSpecification(ownerId);
        var collections = await _unitOfWork.Repository<Collection>().ListAsync(userCollectionsSpec, tracking: false);
        if (collections == null)
        {
            return null;
        }

        foreach (var collection in collections)
        {
            var cardsSpec = new CardsWithParamsSpecification(
                new EntitySpecParams(),
                collection.Id,
                applySorting: false,
                applyPaging: false);
            var collectionCards = await _unitOfWork.Repository<Card>().ListAsync(cardsSpec, tracking: false);
            var ownedCard = collectionCards?.FirstOrDefault(c => string.Equals(c.OracleId, oracleId, StringComparison.OrdinalIgnoreCase));
            if (ownedCard != null)
            {
                return ownedCard;
            }
        }

        return null;
    }

    private async Task RecalculateDeckColorIdentityAsync(int deckId)
    {
        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null)
        {
            return;
        }

        var spec = new DeckCardsWithDeckIdSpecification(deckId);
        var deckCards = await _unitOfWork.Repository<DeckCard>().ListAsync(spec, tracking: false) ?? [];

        var colors = deckCards
            .Where(dc => dc.ColorIdentity != null)
            .SelectMany(dc => dc.ColorIdentity)
            .Where(ci => !string.IsNullOrWhiteSpace(ci))
            .Select(ci => ci.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        deck.ColorIdentity = colors;
        _unitOfWork.Repository<Deck>().Update(deck);
        await _unitOfWork.Complete();
    }

    private async Task EnsureCopyLimitAsync(int deckId, string oracleId, string cardName, string? typeLine, int requestedTotalQuantity, int? existingDeckCardId, string operation)
    {
        if (IsBasicLandTypeLine(typeLine))
        {
            return;
        }

        var spec = new DeckCardsWithDeckIdSpecification(deckId);
        var deckCards = await _unitOfWork.Repository<DeckCard>().ListAsync(spec, tracking: false) ?? [];

        var existingTotal = deckCards
            .Where(dc => string.Equals(dc.OracleId, oracleId, StringComparison.OrdinalIgnoreCase))
            .Where(dc => !existingDeckCardId.HasValue || dc.Id != existingDeckCardId.Value)
            .Sum(dc => dc.MaindeckQuantity + dc.SideboardQuantity);

        if (existingTotal + requestedTotalQuantity > 4)
        {
            _logger.LogOperationWarning(operation, "Copy limit exceeded", new { deckId, cardName, requestedTotalQuantity });
            throw new InvalidOperationException($"Adding '{cardName}' would exceed the 4-copy limit for this deck.");
        }
    }

    private static List<string> NormalizeColorIdentity(IEnumerable<string?>? colorIdentity)
    {
        return (colorIdentity ?? [])
            .Where(ci => !string.IsNullOrWhiteSpace(ci))
            .Select(ci => ci!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private Task<MarketProvider> ResolveMarketProviderAsync(string ownerId)
    {
        return _userSettingsService.GetMarketProviderAsync(ownerId);
    }

    private async Task<MarketProvider> ResolveMarketProviderForDeckAsync(int deckId)
    {
        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(deckId, tracking: false);
        return deck is null ? MarketProvider.Mkm : await ResolveMarketProviderAsync(deck.OwnerId);
    }

    private double? ResolveMarketPrice(string scryfallId, MarketProvider marketProvider)
    {
        if (!_cardDataService.CardDataById.TryGetValue(scryfallId, out var marketData) || marketData?.Prices is null)
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
