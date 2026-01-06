namespace API.Dtos.Collections;

using CsvHelper.Configuration.Attributes;

public class GoldfishCsvRecordDto
{
    [Name("Card")]
    public required string Name { get; set; }

    [Name("Set ID")]
    public required string SetCode { get; set; }

    [Name("Set Name")]
    public string? SetName { get; set; }

    [Name("Quantity")]
    public int Quantity { get; set; }

    [Name("Foil")]
    [BooleanTrueValues("Yes", "Y", "True", "1", "true", "foil", "Foil", "etched")]
    [BooleanFalseValues("No", "N", "False", "0", "false", "", "normal", "Normal", "regular")]
    public bool IsFoil { get; set; }

    [Name("Variation")]
    public string? Variation { get; set; }

    [Name("Collector Number")]
    public required string CollectorNumber { get; set; }

    [Name("Scryfall ID")]
    public string? ScryfallId { get; set; }
}
