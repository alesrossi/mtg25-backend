namespace API.Dtos.Collections;

using CsvHelper.Configuration.Attributes;
using Core.Enums;

public class DelverCsvRecordDto
{
    [Name("Quantity")]
    public int Quantity { get; set; }

    [Name("Name")]
    public required string Name { get; set; }

    [Name("Edition code")]
    public required string SetCode { get; set; }

    [Name("Scryfall ID")]
    public required string ScryfallId { get; set; }

    [Name("Price")]
    public string Price { get; set; } = string.Empty;

    [Name("Language")]
    public string? Language { get; set; }

    [Name("Foil")]
    [BooleanTrueValues("Yes", "Y", "True", "1", "true", "foil", "Foil", "etched")]
    [BooleanFalseValues("No", "N", "False", "0", "false", "", "normal", "Normal", "nonfoil", "Nonfoil")]
    public bool IsFoil { get; set; }

    [Name("Currency")]
    public string? Currency { get; set; }

    [Name("Condition")]
    public string? Condition { get; set; }

}