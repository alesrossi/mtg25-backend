using API.Dtos.Cards;
using Core.Enums;

namespace API.Helpers;

public static class DeckLegalityHelper
{
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
}
