using System.Linq;
using API.Dtos.Cards;
using API.Dtos.Decks;
using API.Logging;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;
using Microsoft.Extensions.Logging;

namespace API.Services;

public class DeckCardService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ILogger<DeckCardService> logger;

    private const string GetDeckCardsOperation = "DeckCards.Fetch";
    private const string CreateDeckCardOperation = "DeckCards.Create";
    private const string UpdateDeckCardOperation = "DeckCards.Update";
    private const string DeleteDeckCardOperation = "DeckCards.Delete";

    public DeckCardService(IUnitOfWork unitOfWork, ILogger<DeckCardService> logger)
    {
        this.unitOfWork = unitOfWork;
        this.logger = logger;
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
        var result = (deckCards ?? []).Select(dc => MapToDtoWithLookup(dc, ownedCardsLookup)).ToList();
        
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

        var mapped = deckCards?.Select(MapToDto).ToList() ?? [];
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
        
        var dto = await MapToDtoAsync(deckCard, deck.OwnerId);
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

        var deckCard = new DeckCard
        {
            DeckId = deckId,
            ScryfallId = createDto.ScryfallId,
            Name = createDto.Name,
            SetCode = createDto.SetCode,
            SetName = createDto.SetName,
            TypeLine = createDto.TypeLine,
            ColorIdentity = (createDto.ColorIdentity ?? new List<string>())
                .Where(ci => !string.IsNullOrWhiteSpace(ci))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
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

        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId, tracking: false);
        if (deck == null)
        {
            logger.LogOperationWarning(CreateDeckCardOperation, "Deck not found after card creation", new { deckId, ScryfallId = createDto.ScryfallId });
            throw new InvalidOperationException("Deck not found");
        }
        
        var dto = await MapToDtoAsync(deckCard, deck.OwnerId);
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
        
        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(updatedDeckCard.DeckId, tracking: false);
        if (deck == null)
        {
            logger.LogOperationWarning(UpdateDeckCardOperation, "Deck missing after deck card update", new { updatedDeckCard.DeckId });
            return null;
        }
        
        var dto = await MapToDtoAsync(updatedDeckCard, deck.OwnerId);
        logger.LogOperationSuccess(UpdateDeckCardOperation, new { id, updatedDeckCard.DeckId });
        return dto;
    }
    
    public async Task<DeckCardDto?> UpdateDeckCardVersionAsync(int id, UpdateDeckCardVersionDto updateDto, ScryfallCardDto scryfallCard, CardDataService cardDataService)
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

        unitOfWork.Repository<DeckCard>().Update(deckCard);
        await unitOfWork.Complete();

        // Reload the entity to get the updated values
        var updatedDeckCard = await unitOfWork.Repository<DeckCard>().GetByIdAsync(id, tracking: false);
        if (updatedDeckCard == null)
        {
            logger.LogOperationWarning(UpdateDeckCardOperation, "Deck card missing after update", new { id });
            return null;
        }
        
        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(updatedDeckCard.DeckId, tracking: false);
        if (deck == null)
        {
            logger.LogOperationWarning(UpdateDeckCardOperation, "Deck missing after deck card update", new { updatedDeckCard.DeckId });
            return null;
        }
        
        var dto = await MapToDtoAsync(updatedDeckCard, deck.OwnerId);
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

        unitOfWork.Repository<DeckCard>().Delete(deckCard);
        await unitOfWork.Complete();

        logger.LogOperationSuccess(DeleteDeckCardOperation, new { id, deckCard.DeckId });
        return true;
    }

    private static DeckCardDto MapToDto(DeckCard deckCard)
    {
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
            OwnedQuantity = deckCard.OwnedCard?.Quantity ?? 0
        };
    }

    private async Task<DeckCardDto> MapToDtoAsync(DeckCard deckCard, string ownerId)
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
            OwnedQuantity = totalOwnedQuantity
        };
    }

    private static DeckCardDto MapToDtoWithLookup(DeckCard deckCard, Dictionary<string, List<Card>> ownedCardsLookup)
    {
        var ownedCards = ownedCardsLookup.TryGetValue(deckCard.ScryfallId, out var cards) ? cards : new List<Card>();
        var totalOwnedQuantity = ownedCards.Sum(c => c.Quantity);
        var firstOwnedCard = ownedCards.FirstOrDefault();
        
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
            OwnedQuantity = totalOwnedQuantity
        };
    }
}
