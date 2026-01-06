namespace API.Dtos.Collections;

using CsvHelper.Configuration.Attributes;

public class DragonshieldCsvRecordDto
{
    [Name("Quantity")]
    public int Quantity { get; set; }

    [Name("Card Name")]
    public required string Name { get; set; }

    [Name("Set Code")]
    public required string SetCode { get; set; }

    [Name("Set Name")]
    public string? SetName { get; set; }

    [Name("Card Number")]
    public required string CollectorNumber { get; set; }

    [Name("Condition")]
    public string? Condition { get; set; }

    [Name("Printing")]
    [BooleanTrueValues("Foil", "foil", "etched")]
    [BooleanFalseValues("Normal", "normal", "", "Nonfoil", "nonfoil")]
    public bool IsFoil { get; set; }

    [Name("Language")]
    public string? Language { get; set; }

    [Name("Price Bought")]
    public string PurchasePrice { get; set; } = string.Empty;
}
