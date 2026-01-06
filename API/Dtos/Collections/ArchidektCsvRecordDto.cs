namespace API.Dtos.Collections;

using CsvHelper.Configuration.Attributes;

public class ArchidektCsvRecordDto
{
    [Name("Quantity")]
    public int Quantity { get; set; }

    [Name("Name")]
    public required string Name { get; set; }

    [Name("Finish")]
    [BooleanTrueValues("Foil", "foil", "etched")]
    [BooleanFalseValues("Normal", "normal", "", "Nonfoil", "nonfoil")]
    public bool IsFoil { get; set; }

    [Name("Condition")]
    public string? Condition { get; set; }

    [Name("Date Added")]
    public string? DateAdded { get; set; }

    [Name("Language")]
    public string? Language { get; set; }

    [Name("Purchase Price")]
    public string PurchasePrice { get; set; } = string.Empty;

    [Name("Tags")]
    public string? Tags { get; set; }

    [Name("Edition Name")]
    public string? EditionName { get; set; }

    [Name("Edition Code")]
    public string? EditionCode { get; set; }

    [Name("Multiverse Id")]
    public string? MultiverseId { get; set; }

    [Name("Scryfall ID")]
    public string? ScryfallId { get; set; }

    [Name("Collector Number")]
    public string? CollectorNumber { get; set; }
}
