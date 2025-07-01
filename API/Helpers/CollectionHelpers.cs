using System.Globalization;
using API.Dtos;
using API.Scryfall;
using Core.Models;
using CsvHelper;

namespace API.Helpers;

public class CollectionHelpers
{
    public static async Task<List<Card>> ProcessCsvFIle(IFormFile file, CardDataService cds, int collectionId)
    {
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        
        
        
        var cardList = new List<Card>();
        
        foreach (var record in csv.GetRecords<CsvRecordDto>())
        {
            if (!cds.CardDataById.TryGetValue(record.ScryfallId, out var ocd)) continue;
            cardList.Add(new Card
            {
                Name = record.Name,
                OracleId = ocd.Id,
                Quantity = record.Quantity,
                Language = record.Language,
                Version = record.CollectorNumber,
                IsFoil = record.IsFoil,
                PurchasePrice = record.PurchasePrice,
                PurchasePriceCurrency = record.PurchasePriceCurrency,
                ImageUrl = ocd.ImageUris.Large,
                SetCode = record.SetCode,
                SetName = record.SetName,
                CollectorNumber = record.CollectorNumber,
                Rarity = record.Rarity,
                IsMisprint = record.IsMisprint,
                IsAltered = record.IsAltered,
                CollectionId = collectionId
            });
        }
        return cardList;
    }
}