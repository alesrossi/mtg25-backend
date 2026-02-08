using API.Dtos.Cards;
using Core.Enums;

namespace API.Helpers;

public static class DeckLegalityHelper
{
    private static readonly HashSet<string> AlwaysOwnedNames = new([
        "Island",
        "Forest",
        "Mountain",
        "Swamp",
        "Plains"
    ], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AnyAmountCardNames = new([
        "Island",
        "Forest",
        "Mountain",
        "Swamp",
        "Plains",
        "Wastes",
        "Snow-Covered Island",
        "Snow-Covered Forest",
        "Snow-Covered Mountain",
        "Snow-Covered Swamp",
        "Snow-Covered Plains",
        "Snow-Covered Wastes",
        "Cid, Timeless Artificer",
        "Dragon's Approach",
        "Hare Apparent",
        "Persistent Petitioners",
        "Rat Colony",
        "Relentless Rats",
        "Shadowborn Apostle",
        "Slime Against Humanity",
        "Tempest Hawk",
        "Templar Knight"
        
    ], StringComparer.OrdinalIgnoreCase);
    
    public static bool IsLegal(DeckFormat format, ScryfallCardDto card)
    {
        if (format == DeckFormat.Limited)
        {
            return true;
        }

        var legalities = card.Legalities;
        if (legalities is null)
        {
            return false;
        }

        var status = format switch
        {
            DeckFormat.Standard => legalities.Standard,
            DeckFormat.Pioneer => legalities.Pioneer,
            DeckFormat.Modern => legalities.Modern,
            DeckFormat.Legacy => legalities.Legacy,
            DeckFormat.Vintage => legalities.Vintage,
            DeckFormat.Pauper => legalities.Pauper,
            DeckFormat.Commander => legalities.Commander,
            DeckFormat.Penny => legalities.Penny,
            DeckFormat.Premodern => legalities.Premodern,
            DeckFormat.Oathbreaker => legalities.OathBreaker,
            DeckFormat.Canadian => legalities.Vintage,
            _ => null
        };

        return string.Equals(status, "legal", StringComparison.OrdinalIgnoreCase);
    }
    
    public static bool IsAlwaysOwned(string? cardName)
    {
        return !string.IsNullOrWhiteSpace(cardName) && AlwaysOwnedNames.Contains(cardName.Trim());
    }

    public static bool IsAnyAmount(string cardName)
    {
        var trimmed = cardName.Trim();
        return AnyAmountCardNames.Contains(trimmed);
    }
    
}
