using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using API.Dtos.Collections;
using API.Scryfall;
using API.Services;
using Core.Models;
using CsvHelper;
using Microsoft.AspNetCore.Http;

namespace API.Helpers;

public static class CollectionHelpers
{
    public static async Task<CollectionImportResult> ProcessCsvFIle(IFormFile file, CardDataService cds, int collectionId)
    {
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        var importedCards = new List<Card>();
        var errors = new List<string>();
        var skippedLines = 0;

        while (csv.Read())
        {
            CsvRecordDto record;
            try
            {
                record = csv.GetRecord<CsvRecordDto>();
            }
            catch (Exception ex)
            {
                skippedLines++;
                errors.Add($"Line {csv.Context.Parser.Row}: {ex.Message}");
                continue;
            }

            if (!cds.CardDataById.TryGetValue(record.ScryfallId, out var ocd))
            {
                skippedLines++;
                errors.Add($"Line {csv.Context.Parser.Row}: Card with Scryfall ID '{record.ScryfallId}' was not found.");
                continue;
            }

            var imageUris = cds.ResolveImageUris(ocd);
            if (imageUris is null)
            {
                skippedLines++;
                errors.Add($"Line {csv.Context.Parser.Row}: Card '{ocd.Name}' is missing image data.");
                continue;
            }

            var imageUrl = imageUris.Large ?? imageUris.Normal ?? imageUris.Png;
            var artCrop = imageUris.ArtCrop;
            var backImageUrl = cds.ResolveBackImageUrl(ocd);

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                skippedLines++;
                errors.Add($"Line {csv.Context.Parser.Row}: Card '{ocd.Name}' is missing image URL.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(artCrop))
            {
                skippedLines++;
                errors.Add($"Line {csv.Context.Parser.Row}: Card '{ocd.Name}' is missing art crop image.");
                continue;
            }

            importedCards.Add(new Card
            {
                Name = record.Name,
                OracleId = ocd.Id,
                Quantity = record.Quantity,
                Language = record.Language,
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
                ArtCrop = artCrop,
                BackImageUrl = backImageUrl
            });
        }

        return new CollectionImportResult(importedCards, errors, skippedLines);
    }
}
