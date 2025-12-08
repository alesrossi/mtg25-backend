namespace Core.Models.Identity;

public class Settings : BaseModel
{
    public MarketProvider? MarketProvider { get; set; }
    public ReferencePrice? ReferencePrice { get; set; } 
    public Currency? Currency { get; set; } 
    public string? LanguageUi { get; set; } = "It";
    public string? LanguageCards { get; set; } = "En";
    public bool? EnabledLocation { get; set; } = false;
    public required string AppUserId { get; set; }
    public required AppUser AppUser { get; set; }
}

public enum ReferencePrice
{
    Min,
    Avg,
    Max
}

public enum MarketProvider
{
    Mkm,
    Tcg
}

public enum Currency
{
    Eur,
    Usd,
    Jyn
}
