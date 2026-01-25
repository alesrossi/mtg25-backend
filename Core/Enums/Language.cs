namespace Core.Enums;

public enum Language
{
    En,
    It
}

public static class LanguageExtensions
{
    public static string ToCode(this Language language)
    {
        return language switch
        {
            Language.En => "en",
            Language.It => "it",
            _ => "en"
        };
    }

    public static bool TryParse(string? value, out Language language)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            language = default;
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.StartsWith("en"))
        {
            language = Language.En;
            return true;
        }

        if (normalized.StartsWith("it"))
        {
            language = Language.It;
            return true;
        }

        switch (normalized)
        {
            case "english":
            case "eng":
                language = Language.En;
                return true;
            case "italian":
            case "italiano":
            case "ita":
                language = Language.It;
                return true;
        }

        language = default;
        return false;
    }

    public static Language ParseOrDefault(string? value, Language defaultLanguage = Language.En)
    {
        return TryParse(value, out var language) ? language : defaultLanguage;
    }

    public static Language? ParseNullable(string? value)
    {
        return TryParse(value, out var language) ? language : null;
    }
}
