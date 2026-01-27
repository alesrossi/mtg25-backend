namespace API.Dtos.Cards;

using System.Collections.Generic;

public record ScryfallCardDto(
    string? Object,
    string Id,
    string OracleId,
    List<int>? MultiverseIds,
    int? MtgoId,
    int? TcgPlayerId,
    int? CardMarketId,
    string Name,
    string? Lang,
    DateTime? ReleasedAt,
    string? Uri,
    string? ScryfallUri,
    string? Layout,
    bool? HighResImage,
    string? ImageStatus,
    ImageUris? ImageUris,
    string? ManaCost,
    float? Cmc,
    string? TypeLine,
    string? OracleText,
    string? Power,
    string? Toughness,
    List<string?> Colors,
    List<string?> ColorIdentity,
    List<string?> Keywords,
    List<CardFace>? CardFaces,
    List<RelatedCard?> AllParts,
    Legalities Legalities,
    List<string?> Games,
    bool? Reserved,
    bool? GameChanger,
    bool? Foil,
    bool? NonFoil,
    List<string?> Finishes,
    bool? Oversized,
    bool? Promo,
    bool? Reprint,
    bool? Variation,
    string? SetId,
    string Set,
    string SetName,
    string? SetType,
    string? SetUri,
    string? SetSearchUri,
    string? ScryfallSetUri,
    string? RulingsUri,
    string? PrintsSearchUri,
    string? CollectorNumber,
    bool? Digital,
    string? Rarity,
    string? Watermark,
    string? FlavorText,
    string? CardBackId,
    Prices? Prices
);

// Nested classes to represent "image_uris," "all_parts," and "legalities"
public record ImageUris(string? Small,
    string? Normal, 
    string? Large, 
    string? Png, 
    string? ArtCrop, 
    string? BorderCrop
);

public record CardFace(
    string Object,
    string Name,
    string ManaCost,
    string TypeLine,
    string OracleText,
    List<string> Colors,
    string FlavorText,
    string Artist,
    string ArtistId,
    string IllustrationId,
    ImageUris ImageUris
);

public record RelatedCard(
    object Object, 
    string? Id, 
    string? Name, 
    string? ImageUris
);

public record Legalities(
    string? Standard,
    string? Future,
    string? Historic,
    string? Timeless,
    string? Gladiator,
    string? Pioneer,
    string? Explorer,
    string? Modern,
    string? Legacy,
    string? Pauper,
    string? Vintage,
    string? OathBreaker,
    string? StandardBrawl,
    string? Brawl,
    string? Alchemy,
    string? PauperCommander,
    string? Duel,
    string? Oldschool,
    string? Premodern,
    string? Predh
);

public record Prices (string? Usd,
    string? UsdFoil, 
    string? Eur, 
    string? EurFoil, 
    string? Tix
);

