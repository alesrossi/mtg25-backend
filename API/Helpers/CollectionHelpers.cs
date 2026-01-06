using System.Globalization;
using System.Linq;
using API.Dtos.Cards;
using API.Dtos.Collections;
using API.Services;
using Core.Models;
using Core.Models.Identity;
using CsvHelper;

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
                errors.Add($"Line {csv.Context.Parser!.Row}: {ex.Message}");
                continue;
            }

            if (!cds.CardDataById.TryGetValue(record.ScryfallId, out var ocd))
            {
                skippedLines++;
                errors.Add($"Line {csv.Context.Parser!.Row}: Card with Scryfall ID '{record.ScryfallId}' was not found.");
                continue;
            }

            var imageUris = CardDataService.ResolveImageUris(ocd);

            var imageUrl = imageUris.Large ?? imageUris.Normal ?? imageUris.Png;
            var artCrop = imageUris.ArtCrop;
            var backImageUrl = cds.ResolveBackImageUrl(ocd);
            var cardName = ocd.Name;
            var setCode = ocd.Set;
            var setName = ocd.SetName;
            var collectorNumber = ocd.CollectorNumber ?? string.Empty;
            var rarity = ocd.Rarity ?? string.Empty;
            var typeLine = ocd.TypeLine ?? string.Empty;

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                skippedLines++;
                errors.Add($"Line {csv.Context.Parser!.Row}: Card '{ocd.Name}' is missing image URL.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(artCrop))
            {
                skippedLines++;
                errors.Add($"Line {csv.Context.Parser!.Row}: Card '{ocd.Name}' is missing art crop image.");
                continue;
            }

            var (purchasePrice, purchaseCurrency) = ResolvePurchasePrice(
                record.PurchasePrice,
                record.PurchasePriceCurrency,
                record.IsFoil,
                ocd,
                marketProvider,
                userCurrency);

            importedCards.Add(new Card
            {
                Name = cardName,
                ScryfallId = ocd.Id,
                Quantity = record.Quantity,
                Language = record.Language,
                IsFoil = record.IsFoil,
                PurchasePrice = purchasePrice,
                PurchasePriceCurrency = purchaseCurrency,
                ImageUrl = imageUrl,
                SetCode = setCode,
                SetName = setName,
                TypeLine = typeLine,
                CollectorNumber = collectorNumber,
                Rarity = rarity,
                IsMisprint = record.IsMisprint,
                IsAltered = record.IsAltered,
                CollectionId = collectionId,
                ArtCrop = artCrop,
                BackImageUrl = backImageUrl
            });
        }

        return new CollectionImportResult(importedCards, errors, skippedLines);
    }

    public static async Task<CollectionImportResult> ProcessMoxfieldCsvFile(
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
            MoxfieldCsvRecordDto record;
            try
            {
                record = csv.GetRecord<MoxfieldCsvRecordDto>();
            }
            catch (Exception ex)
            {
                skippedLines++;
                errors.Add($"Line {csv.Context.Parser!.Row}: {ex.Message}");
                continue;
            }

            var normalizedName = record.Name?.Trim() ?? string.Empty;
            var normalizedSet = record.SetCode?.Trim() ?? string.Empty;
            var normalizedCollector = record.CollectorNumber?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedName) ||
                string.IsNullOrWhiteSpace(normalizedSet) ||
                string.IsNullOrWhiteSpace(normalizedCollector))
            {
                skippedLines++;
                errors.Add($"Line {csv.Context.Parser!.Row}: Missing required card data (Name, Edition, or Collector Number).");
                continue;
            }

            var matchingCards = cds.CardDataById.Values
                .Where(card =>
                    string.Equals(card.Name, normalizedName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(card.Set, normalizedSet, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(card.CollectorNumber ?? string.Empty, normalizedCollector, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList();

            if (matchingCards.Count != 1)
            {
                skippedLines++;
                var reason = matchingCards.Count == 0
                    ? $"Card '{record.Name}' with set '{record.SetCode}' and collector number '{record.CollectorNumber}' was not found."
                    : $"Multiple cards matched '{record.Name}' with set '{record.SetCode}' and collector number '{record.CollectorNumber}'.";
                errors.Add($"Line {csv.Context.Parser!.Row}: {reason}");
                continue;
            }

            var ocd = matchingCards[0];

            var imageUris = CardDataService.ResolveImageUris(ocd);
            var imageUrl = imageUris.Large ?? imageUris.Normal ?? imageUris.Png;
            var artCrop = imageUris.ArtCrop;

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                skippedLines++;
                errors.Add($"Line {csv.Context.Parser!.Row}: Card '{ocd.Name}' is missing image URL.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(artCrop))
            {
                skippedLines++;
                errors.Add($"Line {csv.Context.Parser!.Row}: Card '{ocd.Name}' is missing art crop image.");
                continue;
            }

            var purchasePriceInput = ParsePurchasePrice(record.PurchasePrice);

            var (purchasePrice, purchaseCurrency) = ResolvePurchasePrice(
                purchasePriceInput,
                string.Empty,
                record.IsFoil,
                ocd,
                marketProvider,
                userCurrency);

            var backImageUrl = cds.ResolveBackImageUrl(ocd);
            var language = record.Language?.Trim() ?? string.Empty;

            importedCards.Add(new Card
            {
                Name = ocd.Name,
                ScryfallId = ocd.Id,
                Quantity = record.Quantity,
                Language = language,
                IsFoil = record.IsFoil,
                PurchasePrice = purchasePrice,
                PurchasePriceCurrency = purchaseCurrency,
                ImageUrl = imageUrl,
                SetCode = ocd.Set,
                SetName = ocd.SetName,
                TypeLine = ocd.TypeLine ?? string.Empty,
                CollectorNumber = ocd.CollectorNumber ?? string.Empty,
                Rarity = ocd.Rarity ?? string.Empty,
                IsMisprint = false,
                IsAltered = record.IsAltered,
                CollectionId = collectionId,
                ArtCrop = artCrop,
                BackImageUrl = backImageUrl
            });
        }

        return new CollectionImportResult(importedCards, errors, skippedLines);
    }

    private static (double price, string currency) ResolvePurchasePrice(
        double purchasePrice,
        string? purchaseCurrency,
        bool isFoil,
        ScryfallCardDto cardData,
        MarketProvider marketProvider,
        Currency userCurrency)
    {
        var price = purchasePrice;
        var currency = purchaseCurrency;

        if (price <= 0)
        {
            var marketPrice = ResolveMarketPrice(cardData, isFoil, marketProvider);
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

    private static double ParsePurchasePrice(string? priceText)
    {
        if (string.IsNullOrWhiteSpace(priceText))
        {
            return 0;
        }

        return double.TryParse(priceText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
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
            : null;
    }

    private static string ConvertCurrencyToCode(Currency currency)
    {
        return currency.ToString().ToUpperInvariant();
    }
}
