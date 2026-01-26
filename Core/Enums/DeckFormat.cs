using Core.Enums;
namespace Core.Enums;

public enum DeckFormat
{
    Standard,
    Pioneer,
    Modern,
    Legacy,
    Vintage,
    Pauper,
    Commander,
    Penny,
    Premodern,
    Oathbreaker,
    Limited,
    Canadian
}

public static class DeckFormatExtensions
{
    private static readonly Dictionary<string, DeckFormat> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Standard"] = DeckFormat.Standard,
        ["Pioneer"] = DeckFormat.Pioneer,
        ["Modern"] = DeckFormat.Modern,
        ["Legacy"] = DeckFormat.Legacy,
        ["Vintage"] = DeckFormat.Vintage,
        ["Pauper"] = DeckFormat.Pauper,
        ["Commander"] = DeckFormat.Commander,
        ["Penny"] = DeckFormat.Penny,
        ["Premodern"] = DeckFormat.Premodern,
        ["Oathbreaker"] = DeckFormat.Oathbreaker,
        ["Limited"] = DeckFormat.Limited,
        ["Canadian"] = DeckFormat.Canadian,
        ["Canadian Highlander"] = DeckFormat.Canadian,
        ["Canlander"] = DeckFormat.Canadian,
        ["Draft"] = DeckFormat.Limited,
        ["Sealed"] = DeckFormat.Limited
    };

    public static string ToCode(this DeckFormat format)
    {
        return format switch
        {
            DeckFormat.Standard => "Standard",
            DeckFormat.Pioneer => "Pioneer",
            DeckFormat.Modern => "Modern",
            DeckFormat.Legacy => "Legacy",
            DeckFormat.Vintage => "Vintage",
            DeckFormat.Pauper => "Pauper",
            DeckFormat.Commander => "Commander",
            DeckFormat.Penny => "Penny",
            DeckFormat.Premodern => "Premodern",
            DeckFormat.Oathbreaker => "Oathbreaker",
            DeckFormat.Limited => "Limited",
            DeckFormat.Canadian => "Canadian",
            _ => "Standard"
        };
    }

    public static bool TryParse(string? value, out DeckFormat format)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            format = default;
            return false;
        }

        var normalized = value.Trim();
        if (Map.TryGetValue(normalized, out format))
        {
            return true;
        }

        format = default;
        return false;
    }

    public static DeckFormat ParseOrDefault(string? value, DeckFormat defaultFormat = DeckFormat.Standard)
    {
        return TryParse(value, out var format) ? format : defaultFormat;
    }
}