using System.Linq;
using API.Dtos.Decks;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;

namespace API.Services;

public class DeckCardService(IUnitOfWork unitOfWork)
{
    public async Task<IEnumerable<DeckCardDto>> GetDeckCardsAsync(int deckId, bool maindeckOnly = false, bool sideboardOnly = false, bool? ownedOnly = null)
    {
        ISpecification<DeckCard> spec = (maindeckOnly, sideboardOnly) switch
        {
            (true, false) => new DeckCardsWithDeckIdSpecification(deckId, maindeckOnly: true),
            (false, true) => new DeckCardsWithDeckIdSpecification(deckId, maindeckOnly: false, sideboardOnly: true),
            (true, true) => new DeckCardsWithDeckIdSpecification(deckId, maindeckOnly: true, sideboardOnly: true),
            _ => new DeckCardsWithDeckIdSpecification(deckId)
        };
        
        var deckCards = await unitOfWork.Repository<DeckCard>().ListAsync(spec);
        
        // Get the deck owner for collection lookup
        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null) return [];
        
        // Get all oracle IDs from deck cards
        var oracleIds = (deckCards ?? []).Select(dc => dc.OracleId).Distinct().ToList();
        
        // Get user's collections
        var userCollectionsSpec = new CollectionWithOwnerSpecification(deck.OwnerId);
        var userCollections = await unitOfWork.Repository<Collection>().ListAsync(userCollectionsSpec);
        
        // Get all owned cards for all oracle IDs in one query per collection
        var ownedCardsLookup = new Dictionary<string, List<Card>>();
        
        if (userCollections != null)
        {
            foreach (var collection in userCollections)
            {
                var cardsSpec = new CardsWithParamsSpecification(new EntitySpecParams(), collection.Id);
                var collectionCards = await unitOfWork.Repository<Card>().ListAsync(cardsSpec);
                
                var matchingCards = collectionCards?.Where(c => oracleIds.Contains(c.OracleId)).ToList();
                if (matchingCards?.Any() == true)
                {
                    foreach (var card in matchingCards)
                    {
                        if (!ownedCardsLookup.ContainsKey(card.OracleId))
                            ownedCardsLookup[card.OracleId] = new List<Card>();
                        ownedCardsLookup[card.OracleId].Add(card);
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
        
        return result;
    }

    public async Task<IEnumerable<DeckCardDto>> GetDeckCardsByOracleIdAsync(string oracleId, int? deckId = null, string? userId = null)
    {
        ISpecification<DeckCard> spec = deckId.HasValue
            ? new DeckCardsWithOracleIdSpecification(oracleId, deckId.Value)
            : new DeckCardsWithOracleIdSpecification(oracleId, userId!);
            
        var deckCards = await unitOfWork.Repository<DeckCard>().ListAsync(spec);
        
        // For this method we'll keep the old synchronous mapping since we don't have owner context
        return deckCards?.Select(MapToDto) ?? [];
    }

    public async Task<DeckCardDto?> GetDeckCardByIdAsync(int id)
    {
        var deckCard = await unitOfWork.Repository<DeckCard>().GetByIdAsync(id);
        if (deckCard == null) return null;
        
        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(deckCard.DeckId);
        if (deck == null) return null;
        
        return await MapToDtoAsync(deckCard, deck.OwnerId);
    }

    public async Task<DeckCardDto> CreateDeckCardAsync(int deckId, CreateDeckCardDto createDto)
    {
        if (string.IsNullOrWhiteSpace(createDto.OracleId) || 
            string.IsNullOrWhiteSpace(createDto.Name) || 
            string.IsNullOrWhiteSpace(createDto.SetCode) ||
            string.IsNullOrWhiteSpace(createDto.ImageUrl))
        {
            throw new ArgumentException("OracleId, Name, SetCode, and ImageUrl are required");
        }

        var deckCard = new DeckCard
        {
            DeckId = deckId,
            OracleId = createDto.OracleId,
            Name = createDto.Name,
            SetCode = createDto.SetCode,
            SetName = createDto.SetName,
            ColorIdentity = (createDto.ColorIdentity ?? new List<string>())
                .Where(ci => !string.IsNullOrWhiteSpace(ci))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ImageUrl = createDto.ImageUrl,
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
        if (deck == null) throw new InvalidOperationException("Deck not found");
        
        return await MapToDtoAsync(deckCard, deck.OwnerId);
    }

    public async Task<DeckCardDto?> UpdateDeckCardAsync(int id, UpdateDeckCardDto updateDto)
    {
        var deckCard = await unitOfWork.Repository<DeckCard>().GetByIdAsync(id);
        if (deckCard == null) return null;

        deckCard.MaindeckQuantity = updateDto.MaindeckQuantity;
        deckCard.SideboardQuantity = updateDto.SideboardQuantity;
        deckCard.OwnedCardId = updateDto.OwnedCardId;

        unitOfWork.Repository<DeckCard>().Update(deckCard);
        await unitOfWork.Complete();

        // Reload the entity to get the updated values
        var updatedDeckCard = await unitOfWork.Repository<DeckCard>().GetByIdAsync(id);
        if (updatedDeckCard == null) return null;
        
        var deck = await unitOfWork.Repository<Deck>().GetByIdAsync(updatedDeckCard.DeckId);
        if (deck == null) return null;
        
        return await MapToDtoAsync(updatedDeckCard, deck.OwnerId);
    }

    public async Task<bool> DeleteDeckCardAsync(int id)
    {
        var deckCard = await unitOfWork.Repository<DeckCard>().GetByIdAsync(id);
        if (deckCard == null) return false;

        unitOfWork.Repository<DeckCard>().Delete(deckCard);
        await unitOfWork.Complete();

        return true;
    }

    private static DeckCardDto MapToDto(DeckCard deckCard)
    {
        return new DeckCardDto
        {
            Id = deckCard.Id,
            DeckId = deckCard.DeckId,
            OracleId = deckCard.OracleId,
            Name = deckCard.Name,
            SetCode = deckCard.SetCode,
            SetName = deckCard.SetName,
            ColorIdentity = deckCard.ColorIdentity,
            ImageUrl = deckCard.ImageUrl,
            ArtCrop = deckCard.ArtCrop,
            Rarity = deckCard.Rarity,
            CollectorNumber = deckCard.CollectorNumber,
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
        var userCollections = await unitOfWork.Repository<Collection>().ListAsync(userCollectionsSpec);
        
        // Find all cards in those collections that match the OracleId
        var totalOwnedQuantity = 0;
        Card? firstOwnedCard = null;
        
        if (userCollections != null)
        {
            foreach (var collection in userCollections)
            {
                var cardsSpec = new CardsWithParamsSpecification(new EntitySpecParams(), collection.Id);
                var collectionCards = await unitOfWork.Repository<Card>().ListAsync(cardsSpec);
                
                var matchingCards = collectionCards?.Where(c => c.OracleId == deckCard.OracleId).ToList();
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
            OracleId = deckCard.OracleId,
            Name = deckCard.Name,
            SetCode = deckCard.SetCode,
            SetName = deckCard.SetName,
            ColorIdentity = deckCard.ColorIdentity,
            ImageUrl = deckCard.ImageUrl,
            ArtCrop = deckCard.ArtCrop,
            Rarity = deckCard.Rarity,
            CollectorNumber = deckCard.CollectorNumber,
            MaindeckQuantity = deckCard.MaindeckQuantity,
            SideboardQuantity = deckCard.SideboardQuantity,
            OwnedCardId = firstOwnedCard?.Id ?? deckCard.OwnedCardId,
            OwnedQuantity = totalOwnedQuantity
        };
    }

    private static DeckCardDto MapToDtoWithLookup(DeckCard deckCard, Dictionary<string, List<Card>> ownedCardsLookup)
    {
        var ownedCards = ownedCardsLookup.TryGetValue(deckCard.OracleId, out var cards) ? cards : new List<Card>();
        var totalOwnedQuantity = ownedCards.Sum(c => c.Quantity);
        var firstOwnedCard = ownedCards.FirstOrDefault();
        
        return new DeckCardDto
        {
            Id = deckCard.Id,
            DeckId = deckCard.DeckId,
            OracleId = deckCard.OracleId,
            Name = deckCard.Name,
            SetCode = deckCard.SetCode,
            SetName = deckCard.SetName,
            ImageUrl = deckCard.ImageUrl,
            ArtCrop = deckCard.ArtCrop,
            Rarity = deckCard.Rarity,
            CollectorNumber = deckCard.CollectorNumber,
            MaindeckQuantity = deckCard.MaindeckQuantity,
            SideboardQuantity = deckCard.SideboardQuantity,
            OwnedCardId = firstOwnedCard?.Id ?? deckCard.OwnedCardId,
            OwnedQuantity = totalOwnedQuantity
        };
    }
}
