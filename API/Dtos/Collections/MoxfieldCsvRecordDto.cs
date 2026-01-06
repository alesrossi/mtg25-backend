namespace API.Dtos.Collections;

using CsvHelper.Configuration.Attributes;

public class MoxfieldCsvRecordDto
{
    [Name("Count")]
    public int Quantity { get; set; }

    [Name("Tradelist Count")]
    public int TradelistCount { get; set; }

    [Name("Name")]
    public required string Name { get; set; }

    [Name("Edition")]
    public required string SetCode { get; set; }

    [Name("Condition")]
    public required string Condition { get; set; }

    [Name("Language")]
    public required string Language { get; set; }

    [Name("Foil")]
    [BooleanTrueValues("Yes", "Y", "True", "1", "true", "Foil", "foil", "etched")]
    [BooleanFalseValues("No", "N", "False", "0", "false", "", "normal", "Normal")]
    public bool IsFoil { get; set; }

    [Name("Tags")]
    public string? Tags { get; set; }

    [Name("Last Modified")]
    public string? LastModified { get; set; }

    [Name("Collector Number")]
    public required string CollectorNumber { get; set; }

    [Name("Alter")]
    [BooleanTrueValues("Yes", "Y", "True", "1", "true")]
    [BooleanFalseValues("No", "N", "False", "0", "false", "")]
    public bool IsAltered { get; set; }

    [Name("Proxy")]
    [BooleanTrueValues("Yes", "Y", "True", "1", "true")]
    [BooleanFalseValues("No", "N", "False", "0", "false", "")]
    public bool IsProxy { get; set; }

    [Name("Purchase Price")]
    public string PurchasePrice { get; set; } = string.Empty;
}
