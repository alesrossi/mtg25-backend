using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using API.Dtos.Cards;
using API.Dtos.Collections;
using API.Scryfall;
using API.Services;
using Core.Models;
using Core.Models.Identity;
using CsvHelper;
using Microsoft.AspNetCore.Http;

namespace API.Helpers;

public static class CollectionHelpers
{
    public static async Task<CollectionImportResult> ProcessCsvFIle(
        IFormFile file,
        CardDataService cds,
        int collectionId,
        MarketProvider marketProvider,
        Currency userCurrency)
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

            var imageUris = CardDataService.ResolveImageUris(ocd);
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

            var (purchasePrice, purchaseCurrency) = ResolvePurchasePrice(record, ocd, marketProvider, userCurrency);

            importedCards.Add(new Card
            {
                Name = record.Name,
                ScryfallId = ocd.Id,
                Quantity = record.Quantity,
                Language = record.Language,
                IsFoil = record.IsFoil,
                PurchasePrice = purchasePrice,
                PurchasePriceCurrency = purchaseCurrency,
                ImageUrl = imageUrl,
                SetCode = record.SetCode,
                SetName = record.SetName,
                TypeLine = ocd.TypeLine ?? string.Empty,
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

    private static (double price, string currency) ResolvePurchasePrice(
        CsvRecordDto record,
        ScryfallCardDto cardData,
        MarketProvider marketProvider,
        Currency userCurrency)
    {
        var price = record.PurchasePrice;
        var currency = record.PurchasePriceCurrency;

        if (price <= 0)
        {
            var marketPrice = ResolveMarketPrice(cardData, record.IsFoil, marketProvider);
            if (marketPrice.HasValue)
            {
                price = marketPrice.Value;
                currency = ConvertCurrencyToCode(userCurrency);
            }
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            currency = ConvertCurrencyToCode(userCurrency);
        }

        return (price, currency);
    }

    private static double? ResolveMarketPrice(ScryfallCardDto cardData, bool isFoil, MarketProvider marketProvider)
    {
        if (cardData.Prices is null)
        {
            return null;
        }

        var priceText = marketProvider == MarketProvider.Mkm
            ? (isFoil ? cardData.Prices.EurFoil : cardData.Prices.Eur)
            : (isFoil ? cardData.Prices.UsdFoil : cardData.Prices.Usd);

        if (string.IsNullOrWhiteSpace(priceText) && isFoil)
        {
            priceText = marketProvider == MarketProvider.Mkm
                ? cardData.Prices.Eur
                : cardData.Prices.Usd;
        }

        if (string.IsNullOrWhiteSpace(priceText))
        {
            return null;
        }

        return double.TryParse(priceText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : (double?)null;
    }

    private static string ConvertCurrencyToCode(Currency currency)
    {
        return currency.ToString().ToUpperInvariant();
    }
}
