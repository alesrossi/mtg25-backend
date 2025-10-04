using System;
using System.Globalization;
using API.Dtos.Collections;
using API.Scryfall;
using API.Services;
using Core.Models;
using CsvHelper;

namespace API.Helpers;

public static class CollectionHelpers
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

            var imageUris = cds.ResolveImageUris(ocd) ?? throw new InvalidOperationException($"Missing image data for card {ocd.Name}");
            var imageUrl = imageUris.Large ?? imageUris.Normal ?? imageUris.Png ?? throw new InvalidOperationException($"Missing image URL for card {ocd.Name}");
            var artCrop = imageUris.ArtCrop ?? throw new InvalidOperationException($"Missing art crop for card {ocd.Name}");
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
                ImageUrl = imageUrl,
                SetCode = record.SetCode,
                SetName = record.SetName,
                CollectorNumber = record.CollectorNumber,
                Rarity = record.Rarity,
                IsMisprint = record.IsMisprint,
                IsAltered = record.IsAltered,
                CollectionId = collectionId,
                ArtCrop = artCrop
            });
        }
        return cardList;
    }
}
