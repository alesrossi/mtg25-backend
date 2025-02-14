using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace API.Dtos
{
    public class OracleCardDto
    {
        [JsonPropertyName("object")]
        public string Object { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("oracle_id")]
        public string OracleId { get; set; }

        [JsonPropertyName("multiverse_ids")]
        public List<int> MultiverseIds { get; set; }

        [JsonPropertyName("mtgo_id")]
        public int MtgoId { get; set; }

        [JsonPropertyName("tcgplayer_id")]
        public int TcgPlayerId { get; set; }

        [JsonPropertyName("cardmarket_id")]
        public int CardMarketId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("lang")]
        public string Lang { get; set; }

        [JsonPropertyName("released_at")]
        public DateTime ReleasedAt { get; set; }

        [JsonPropertyName("uri")]
        public string Uri { get; set; }

        [JsonPropertyName("scryfall_uri")]
        public string ScryfallUri { get; set; }

        [JsonPropertyName("layout")]
        public string Layout { get; set; }

        [JsonPropertyName("highres_image")]
        public bool HighResImage { get; set; }

        [JsonPropertyName("image_status")]
        public string ImageStatus { get; set; }

        [JsonPropertyName("image_uris")]
        public ImageUris ImageUris { get; set; }

        [JsonPropertyName("mana_cost")]
        public string ManaCost { get; set; }

        [JsonPropertyName("cmc")]
        public float Cmc { get; set; }

        [JsonPropertyName("type_line")]
        public string TypeLine { get; set; }

        [JsonPropertyName("oracle_text")]
        public string OracleText { get; set; }

        [JsonPropertyName("power")]
        public string Power { get; set; }

        [JsonPropertyName("toughness")]
        public string Toughness { get; set; }

        [JsonPropertyName("colors")]
        public List<string> Colors { get; set; }

        [JsonPropertyName("color_identity")]
        public List<string> ColorIdentity { get; set; }

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; }

        [JsonPropertyName("all_parts")]
        public List<RelatedCard> AllParts { get; set; }

        [JsonPropertyName("legalities")]
        public Legalities Legalities { get; set; }

        [JsonPropertyName("games")]
        public List<string> Games { get; set; }

        [JsonPropertyName("reserved")]
        public bool Reserved { get; set; }

        [JsonPropertyName("game_changer")]
        public bool GameChanger { get; set; }

        [JsonPropertyName("foil")]
        public bool Foil { get; set; }

        [JsonPropertyName("nonfoil")]
        public bool NonFoil { get; set; }

        [JsonPropertyName("finishes")]
        public List<string> Finishes { get; set; }

        [JsonPropertyName("oversized")]
        public bool Oversized { get; set; }

        [JsonPropertyName("promo")]
        public bool Promo { get; set; }

        [JsonPropertyName("reprint")]
        public bool Reprint { get; set; }

        [JsonPropertyName("variation")]
        public bool Variation { get; set; }

        [JsonPropertyName("set_id")]
        public string SetId { get; set; }

        [JsonPropertyName("set")]
        public string Set { get; set; }

        [JsonPropertyName("set_name")]
        public string SetName { get; set; }

        [JsonPropertyName("set_type")]
        public string SetType { get; set; }

        [JsonPropertyName("set_uri")]
        public string SetUri { get; set; }

        [JsonPropertyName("set_search_uri")]
        public string SetSearchUri { get; set; }

        [JsonPropertyName("scryfall_set_uri")]
        public string ScryfallSetUri { get; set; }

        [JsonPropertyName("rulings_uri")]
        public string RulingsUri { get; set; }

        [JsonPropertyName("prints_search_uri")]
        public string PrintsSearchUri { get; set; }

        [JsonPropertyName("collector_number")]
        public string CollectorNumber { get; set; }

        [JsonPropertyName("digital")]
        public bool Digital { get; set; }

        [JsonPropertyName("rarity")]
        public string Rarity { get; set; }

        [JsonPropertyName("watermark")]
        public string Watermark { get; set; }

        [JsonPropertyName("flavor_text")]
        public string FlavorText { get; set; }

        [JsonPropertyName("card_back_id")]
        public string CardBackId { get; set; }
    }

    // Nested classes to represent "image_uris," "all_parts," and "legalities"
    public class ImageUris
    {
        [JsonPropertyName("small")]
        public string Small { get; set; }

        [JsonPropertyName("normal")]
        public string Normal { get; set; }

        [JsonPropertyName("large")]
        public string Large { get; set; }

        [JsonPropertyName("png")]
        public string Png { get; set; }

        [JsonPropertyName("art_crop")]
        public string ArtCrop { get; set; }

        [JsonPropertyName("border_crop")]
        public string BorderCrop { get; set; }
    }

    public class RelatedCard
    {
        [JsonPropertyName("object")]
        public string Object { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("component")]
        public string Component { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type_line")]
        public string TypeLine { get; set; }

        [JsonPropertyName("uri")]
        public string Uri { get; set; }
    }

    public class Legalities
    {
        [JsonPropertyName("standard")]
        public string Standard { get; set; }

        [JsonPropertyName("future")]
        public string Future { get; set; }

        [JsonPropertyName("historic")]
        public string Historic { get; set; }

        [JsonPropertyName("timeless")]
        public string Timeless { get; set; }

        [JsonPropertyName("gladiator")]
        public string Gladiator { get; set; }

        [JsonPropertyName("pioneer")]
        public string Pioneer { get; set; }

        [JsonPropertyName("explorer")]
        public string Explorer { get; set; }

        [JsonPropertyName("modern")]
        public string Modern { get; set; }

        [JsonPropertyName("legacy")]
        public string Legacy { get; set; }

        [JsonPropertyName("pauper")]
        public string Pauper { get; set; }

        [JsonPropertyName("vintage")]
        public string Vintage { get; set; }

        [JsonPropertyName("penny")]
        public string Penny { get; set; }

        [JsonPropertyName("commander")]
        public string Commander { get; set; }

        [JsonPropertyName("oathbreaker")]
        public string Oathbreaker { get; set; }

        [JsonPropertyName("standardbrawl")]
        public string StandardBrawl { get; set; }

        [JsonPropertyName("brawl")]
        public string Brawl { get; set; }

        [JsonPropertyName("alchemy")]
        public string Alchemy { get; set; }

        [JsonPropertyName("paupercommander")]
        public string PauperCommander { get; set; }

        [JsonPropertyName("duel")]
        public string Duel { get; set; }

        [JsonPropertyName("oldschool")]
        public string OldSchool { get; set; }

        [JsonPropertyName("premodern")]
        public string Premodern { get; set; }

        [JsonPropertyName("predh")]
        public string Predh { get; set; }
    }
}