using API.Helpers;
using API.Logging;
using Core.Enums;
using Core.Interfaces;
using Core.Models;

namespace API.Services;

public class DeckValidationService : IDeckValidationService
{
    private readonly ILogger<DeckValidationService> _logger;
    private const string ValidateDeckOperation = "Decks.Validate";
    private const string ValidateDeckWithCardsOperation = "Decks.ValidateWithCards";

    public DeckValidationService(ILogger<DeckValidationService> logger)
    {
        _logger = logger;
    }

    private static readonly FormatRules StandardRules = new()
    {
        MinimumCards = 60,
        MaximumCards = null,
        AllowsSideboard = true,
        MaximumSideboardSize = 15,
        MaximumCopiesPerCard = 4,
        IsSingleton = false
    };

    private static readonly FormatRules CommanderRules = new()
    {
        MinimumCards = 100,
        MaximumCards = 100,
        AllowsSideboard = false,
        MaximumSideboardSize = 0,
        MaximumCopiesPerCard = 1,
        IsSingleton = true
    };

    private static readonly FormatRules LimitedRules = new()
    {
        MinimumCards = 40,
        MaximumCards = null,
        AllowsSideboard = true,
        MaximumSideboardSize = null, // No limit for limited sideboard
        MaximumCopiesPerCard = null, // No limit in limited
        IsSingleton = false
    };

    private static readonly Dictionary<DeckFormat, FormatRules> FormatRulesMap = new()
    {
        [DeckFormat.Standard] = StandardRules,
        [DeckFormat.Modern] = StandardRules,
        [DeckFormat.Legacy] = StandardRules,
        [DeckFormat.Pioneer] = StandardRules,
        [DeckFormat.Vintage] = StandardRules,
        [DeckFormat.Pauper] = StandardRules,
        [DeckFormat.Penny] = StandardRules,
        [DeckFormat.Premodern] = StandardRules,
        [DeckFormat.Canadian] = StandardRules,
        [DeckFormat.Oathbreaker] = StandardRules,
        [DeckFormat.Commander] = CommanderRules,
        [DeckFormat.Limited] = LimitedRules
    };

    public Task<DeckValidationResult> ValidateDeckAsync(Deck deck)
    {
        using var scope = _logger.BeginOperationScope(ValidateDeckOperation, deck.Id);
        _logger.LogOperationStart(ValidateDeckOperation, new { deck.Id, deck.Format, deck.NumberOfCards });

        var result = new DeckValidationResult { IsValid = true };
        
        if (!FormatRulesMap.TryGetValue(deck.Format, out var formatRules))
        {
            result.AddError($"Unknown format: {deck.Format}", ValidationErrorType.Format);
            _logger.LogOperationWarning(ValidateDeckOperation, "Unknown format", new { deck.Format });
            return Task.FromResult(result);
        }

        // Validate card count from deck metadata
        ValidateCardCount(deck.NumberOfCards, formatRules, result);
        _logger.LogOperationSuccess(ValidateDeckOperation, new { deck.Id, result.IsValid, Errors = result.Errors.Count });
        return Task.FromResult(result);
    }

    public Task<DeckValidationResult> ValidateDeckWithCardsAsync(Deck deck, IEnumerable<DeckCard> deckCards)
    {
        using var scope = _logger.BeginOperationScope(ValidateDeckWithCardsOperation, deck.Id);
        _logger.LogOperationStart(ValidateDeckWithCardsOperation, new { deck.Id, deck.Format });

        var result = new DeckValidationResult { IsValid = true };
        
        if (!FormatRulesMap.TryGetValue(deck.Format, out var formatRules))
        {
            result.AddError($"Unknown format: {deck.Format}", ValidationErrorType.Format);
            _logger.LogOperationWarning(ValidateDeckWithCardsOperation, "Unknown format", new { deck.Format });
            return Task.FromResult(result);
        }

        var deckCardsList = deckCards.ToList();
        
        // Calculate actual card counts
        var maindeckCount = deckCardsList.Sum(dc => dc.MaindeckQuantity);
        var sideboardCount = deckCardsList.Sum(dc => dc.SideboardQuantity);
        var totalCards = maindeckCount + sideboardCount;

        // Validate maindeck count
        ValidateCardCount(maindeckCount, formatRules, result);
        
        // Validate sideboard
        ValidateSideboard(sideboardCount, formatRules, result);
        
        // Validate card copies (singleton rules, 4-of rules, etc.)
        ValidateCardCopies(deckCardsList, formatRules, result);
        
        // Check for empty deck
        if (totalCards == 0)
        {
            result.AddError("Deck cannot be empty", ValidationErrorType.CardCount);
        }
        
        _logger.LogOperationSuccess(ValidateDeckWithCardsOperation, new
        {
            deck.Id,
            deck.Format,
            result.IsValid,
            Errors = result.Errors.Count,
            Maindeck = maindeckCount,
            Sideboard = sideboardCount
        });
        return Task.FromResult(result);
    }

    public int GetMinimumCardCount(DeckFormat format)
    {
        return FormatRulesMap.TryGetValue(format, out var rules) ? rules.MinimumCards : 60;
    }

    public int? GetMaximumCardCount(DeckFormat format)
    {
        return FormatRulesMap.TryGetValue(format, out var rules) ? rules.MaximumCards : null;
    }

    public bool FormatAllowsSideboard(DeckFormat format)
    {
        return FormatRulesMap.TryGetValue(format, out var rules) && rules.AllowsSideboard;
    }

    public int GetMaximumSideboardSize(DeckFormat format)
    {
        return FormatRulesMap.TryGetValue(format, out var rules) ? rules.MaximumSideboardSize ?? 0 : 0;
    }

    private static void ValidateCardCount(int cardCount, FormatRules formatRules, DeckValidationResult result)
    {
        if (cardCount < formatRules.MinimumCards)
        {
            result.AddError($"Deck must contain at least {formatRules.MinimumCards} cards (currently has {cardCount})", ValidationErrorType.CardCount);
        }
        
        if (formatRules.MaximumCards.HasValue && cardCount > formatRules.MaximumCards.Value)
        {
            result.AddError($"Deck cannot contain more than {formatRules.MaximumCards.Value} cards (currently has {cardCount})", ValidationErrorType.CardCount);
        }
    }

    private static void ValidateSideboard(int sideboardCount, FormatRules formatRules, DeckValidationResult result)
    {
        if (!formatRules.AllowsSideboard && sideboardCount > 0)
        {
            result.AddError("This format does not allow sideboards", ValidationErrorType.Sideboard);
            return;
        }

        if (formatRules.MaximumSideboardSize.HasValue && sideboardCount > formatRules.MaximumSideboardSize.Value)
        {
            result.AddError($"Sideboard cannot contain more than {formatRules.MaximumSideboardSize.Value} cards (currently has {sideboardCount})", ValidationErrorType.Sideboard);
        }
    }

    private static void ValidateCardCopies(List<DeckCard> deckCards, FormatRules formatRules, DeckValidationResult result)
    {
        if (formatRules.MaximumCopiesPerCard == null) return; // No copy restrictions (Draft/Sealed)

        var maindeckCounts = new Dictionary<string, int>();
        var sideboardCounts = new Dictionary<string, int>();
        
        foreach (var deckCard in deckCards)
        {
            var cardKey = $"{deckCard.Name}|{deckCard.ScryfallId}"; // Use both name and ScryfallId for uniqueness
            
            // Count maindeck copies
            if (deckCard.MaindeckQuantity > 0)
            {
                if (maindeckCounts.ContainsKey(cardKey))
                {
                    maindeckCounts[cardKey] += deckCard.MaindeckQuantity;
                }
                else
                {
                    maindeckCounts[cardKey] = deckCard.MaindeckQuantity;
                }
            }
            
            // Count sideboard copies
            if (deckCard.SideboardQuantity <= 0) continue;
            if (sideboardCounts.ContainsKey(cardKey))
            {
                sideboardCounts[cardKey] += deckCard.SideboardQuantity;
            }
            else
            {
                sideboardCounts[cardKey] = deckCard.SideboardQuantity;
            }
        }

        // Validate maindeck copies
        ValidateCardCountByLocation(maindeckCounts, formatRules, "maindeck", result);
        
        // Validate sideboard copies
        ValidateCardCountByLocation(sideboardCounts, formatRules, "sideboard", result);
        
        // For singleton formats, validate total across maindeck + sideboard
        if (!formatRules.IsSingleton) return;
        {
            var totalCounts = new Dictionary<string, int>();
            
            // Combine all cards from both locations
            foreach (var (cardKey, count) in maindeckCounts)
            {
                totalCounts[cardKey] = count;
            }
            
            foreach (var (cardKey, count) in sideboardCounts)
            {
                if (!totalCounts.TryAdd(cardKey, count))
                {
                    totalCounts[cardKey] += count;
                }
            }
            
            foreach (var (cardKey, count) in totalCounts)
            {
                var cardName = cardKey.Split('|')[0];
                
                // Basic lands are exempt from copy restrictions
                if (DeckLegalityHelper.IsAnyAmount(cardName))
                {
                    continue;
                }
                
                if (count > 1)
                {
                    result.AddError($"Card '{cardName}' appears {count} times total, but this format allows only 1 copy of each card", ValidationErrorType.Singleton);
                }
            }
        }
    }

    private static void ValidateCardCountByLocation(Dictionary<string, int> cardCounts, FormatRules formatRules, string location, DeckValidationResult result)
    {
        foreach (var (cardKey, count) in cardCounts)
        {
            var cardName = cardKey.Split('|')[0];
            
            // Basic lands are exempt from copy restrictions
            if (DeckLegalityHelper.IsAnyAmount(cardName))
            {
                continue;
            }
            
            if (!formatRules.IsSingleton && count > formatRules.MaximumCopiesPerCard!.Value)
            {
                result.AddError($"Card '{cardName}' appears {count} times in {location}, but maximum is {formatRules.MaximumCopiesPerCard.Value} copies", ValidationErrorType.CardLegality);
            }
        }
    }

    private class FormatRules
    {
        public int MinimumCards { get; set; }
        public int? MaximumCards { get; set; }
        public bool AllowsSideboard { get; set; }
        public int? MaximumSideboardSize { get; set; }
        public int? MaximumCopiesPerCard { get; set; }
        public bool IsSingleton { get; set; }
    }
}
