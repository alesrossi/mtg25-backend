namespace Core.Enums;

public enum CardLanguage
{
    En,   // English
    Es,   // Spanish
    Fr,   // French
    De,   // German
    It,   // Italian
    Pt,   // Portuguese
    Ja,   // Japanese
    Ko,   // Korean
    Ru,   // Russian
    Zhs,  // Simplified Chinese
    Zht,  // Traditional Chinese
    He,   // Hebrew
    La,   // Latin
    Grc,  // Ancient Greek
    Ar,   // Arabic
    Sa,   // Sanskrit
    Ph,   // Phyrexian
    Qya   // Quenya
}

public static class CardLanguageExtensions
{
    public static string ToCode(this CardLanguage language) => language switch
    {
        CardLanguage.En  => "en",
        CardLanguage.Es  => "es",
        CardLanguage.Fr  => "fr",
        CardLanguage.De  => "de",
        CardLanguage.It  => "it",
        CardLanguage.Pt  => "pt",
        CardLanguage.Ja  => "ja",
        CardLanguage.Ko  => "ko",
        CardLanguage.Ru  => "ru",
        CardLanguage.Zhs => "zhs",
        CardLanguage.Zht => "zht",
        CardLanguage.He  => "he",
        CardLanguage.La  => "la",
        CardLanguage.Grc => "grc",
        CardLanguage.Ar  => "ar",
        CardLanguage.Sa  => "sa",
        CardLanguage.Ph  => "ph",
        CardLanguage.Qya => "qya",
        _                => "en"
    };

    public static bool TryParse(string? value, out CardLanguage language)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            language = default;
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();

        language = normalized switch
        {
            // Scryfall codes
            "en"                                  => CardLanguage.En,
            "es" or "sp"                          => CardLanguage.Es,
            "fr"                                  => CardLanguage.Fr,
            "de"                                  => CardLanguage.De,
            "it"                                  => CardLanguage.It,
            "pt"                                  => CardLanguage.Pt,
            "ja" or "jp"                          => CardLanguage.Ja,
            "ko" or "kr"                          => CardLanguage.Ko,
            "ru"                                  => CardLanguage.Ru,
            "zhs" or "cs"                         => CardLanguage.Zhs,
            "zht" or "ct"                         => CardLanguage.Zht,
            "he"                                  => CardLanguage.He,
            "la"                                  => CardLanguage.La,
            "grc" or "ag"                         => CardLanguage.Grc,
            "ar"                                  => CardLanguage.Ar,
            "sa"                                  => CardLanguage.Sa,
            "ph"                                  => CardLanguage.Ph,
            "qya"                                 => CardLanguage.Qya,

            // Full language names
            "english"                             => CardLanguage.En,
            "spanish" or "español" or "espanol"   => CardLanguage.Es,
            "french" or "français" or "francais"  => CardLanguage.Fr,
            "german" or "deutsch"                 => CardLanguage.De,
            "italian" or "italiano"               => CardLanguage.It,
            "portuguese" or "português"           => CardLanguage.Pt,
            "japanese" or "日本語"                => CardLanguage.Ja,
            "korean" or "한국어"                  => CardLanguage.Ko,
            "russian" or "русский"               => CardLanguage.Ru,
            "simplified chinese" or "chinese simplified" or "简体中文" => CardLanguage.Zhs,
            "traditional chinese" or "chinese traditional" or "繁體中文" => CardLanguage.Zht,
            "hebrew"                              => CardLanguage.He,
            "latin"                               => CardLanguage.La,
            "ancient greek" or "greek"            => CardLanguage.Grc,
            "arabic"                              => CardLanguage.Ar,
            "sanskrit"                            => CardLanguage.Sa,
            "phyrexian"                           => CardLanguage.Ph,
            "quenya"                              => CardLanguage.Qya,

            _ => (CardLanguage)(-1)
        };

        if ((int)language != -1) return true;
        language = default;
        return false;

    }

    public static CardLanguage ParseOrDefault(string? value, CardLanguage defaultLanguage = CardLanguage.En)
        => TryParse(value, out var language) ? language : defaultLanguage;

    public static CardLanguage? ParseNullable(string? value)
        => TryParse(value, out var language) ? language : null;
}
