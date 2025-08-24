namespace API.Dtos;

using CsvHelper.Configuration.Attributes;

public abstract class CsvRecordDto
{
    [Name("Name")]
    public required string Name { get; set; }
    
    [Name("Set code")]
    public required string SetCode { get; set; }
    
    [Name("Set name")]
    public required string SetName { get; set; }
    
    [Name("Collector number")]
    public required string CollectorNumber { get; set; }
    
    [Name("Foil")]
    [BooleanTrueValues("Yes", "Y", "True", "1", "true", "foil")]
    [BooleanFalseValues("No", "N", "False", "0", "false", "", "normal")]
    public bool IsFoil { get; set; }
    
    [Name("Rarity")]
    public required string Rarity { get; set; }
    
    [Name("Quantity")]
    public int Quantity { get; set; }
    
    [Name("ManaBox ID")]
    public string? ManaBoxId { get; set; }
    
    [Name("Scryfall ID")]
    public required string ScryfallId { get; set; }
    
    [Name("Purchase price")]
    public double PurchasePrice { get; set; }
    
    [Name("Misprint")]
    [BooleanTrueValues("Yes", "Y", "True", "1", "true")]
    [BooleanFalseValues("No", "N", "False", "0", "false", "")]
    public bool IsMisprint { get; set; }
    
    [Name("Altered")]
    [BooleanTrueValues("Yes", "Y", "True", "1", "true")]
    [BooleanFalseValues("No", "N", "False", "0", "false", "")]
    public bool IsAltered { get; set; }
    
    [Name("Condition")]
    public required string Condition { get; set; }
    
    [Name("Language")]
    public required string Language { get; set; }
    
    [Name("Purchase price currency")]
    public required string PurchasePriceCurrency { get; set; }
}