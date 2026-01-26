using Core.Enums;
namespace Core.Enums;

public enum Currency
{
    Eur,
    Usd
}

public static class CurrencyExtensions
{
    public static string ToCode(this Currency currency)
    {
        return currency switch
        {
            Currency.Eur => "EUR",
            Currency.Usd => "USD",
            _ => "USD"
        };
    }

    public static bool TryParse(string? value, out Currency currency)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            currency = default;
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "eur":
            case "euro":
            case "€":
                currency = Currency.Eur;
                return true;
            case "usd":
            case "us$":
            case "$":
                currency = Currency.Usd;
                return true;
        }

        currency = default;
        return false;
    }

    public static Currency ParseOrDefault(string? value, Currency defaultCurrency = Currency.Usd)
    {
        return TryParse(value, out var currency) ? currency : defaultCurrency;
    }

    public static Currency? ParseNullable(string? value)
    {
        return TryParse(value, out var currency) ? currency : null;
    }
}