using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using API.Dtos.Cards;
using API.Dtos.Collections;
using API.Services;
using Core.Enums;
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
        using var streamReader = new StreamReader(stream);
        TextReader reader = streamReader;
        var firstLine = await streamReader.ReadLineAsync();
        var firstLineForCheck = firstLine?.TrimStart('\uFEFF', ' ', '\t');
        if (!string.IsNullOrWhiteSpace(firstLineForCheck) &&
            (firstLineForCheck.StartsWith("sep=", StringComparison.OrdinalIgnoreCase) ||
             firstLineForCheck.StartsWith("\"sep=", StringComparison.OrdinalIgnoreCase)))
        {
            // skip separator declaration
        }
        else if (firstLine != null)
        {
            var remainder = await streamReader.ReadToEndAsync();
            reader = new StringReader(firstLine + Environment.NewLine + remainder);
        }

        var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

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
            var backImageUrl = CardDataService.ResolveBackImageUrl(ocd);
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
                CurrencyExtensions.ParseNullable(record.PurchasePriceCurrency),
                record.IsFoil,
                ocd,
                marketProvider,
                userCurrency);

            importedCards.Add(new Card
            {
                Name = cardName,
                ScryfallId = ocd.Id,
                OracleId = CardDataService.ResolveOracleId(ocd) ?? string.Empty,
                Quantity = record.Quantity,
                Language = CardLanguageExtensions.ParseOrDefault(record.Language),
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

        while (await csv.ReadAsync())
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

            if (!TryResolveCardByPrinting(cds, normalizedName, normalizedSet, normalizedCollector, out var ocd, out var lookupError))
            {
                skippedLines++;
                errors.Add($"Line {csv.Context.Parser!.Row}: {lookupError}");
                continue;
            }

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

            var (purchasePrice, purchaseCurrency) = ResolvePurchasePrice(
                0,
                null,
                record.IsFoil,
                ocd,
                marketProvider,
                userCurrency);

            var backImageUrl = CardDataService.ResolveBackImageUrl(ocd);
            const CardLanguage language = CardLanguage.En;
            const Condition defaultCondition = Condition.NearMint;

            importedCards.Add(new Card
            {
                Name = ocd.Name,
                ScryfallId = ocd.Id,
                OracleId = CardDataService.ResolveOracleId(ocd) ?? string.Empty,
                Quantity = record.Quantity,
                Language = language,
                Condition = defaultCondition,
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

    public static async Task<CollectionImportResult> ProcessGoldfishCsvFile(
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

        while (await csv.ReadAsync())
        {
            GoldfishCsvRecordDto record;
            try
            {
                record = csv.GetRecord<GoldfishCsvRecordDto>();
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
            var normalizedScryfallId = record.ScryfallId?.Trim();

            ScryfallCardDto? ocd;

            if (!string.IsNullOrWhiteSpace(normalizedScryfallId))
            {
                if (!cds.CardDataById.TryGetValue(normalizedScryfallId, out ocd))
                {
                    skippedLines++;
                    errors.Add($"Line {csv.Context.Parser!.Row}: Card with Scryfall ID '{normalizedScryfallId}' was not found.");
                    continue;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(normalizedName) ||
                    string.IsNullOrWhiteSpace(normalizedSet) ||
                    string.IsNullOrWhiteSpace(normalizedCollector))
                {
                    skippedLines++;
                    errors.Add($"Line {csv.Context.Parser!.Row}: Missing required card data (Card, Set ID, or Collector Number).");
                    continue;
                }

                if (!TryResolveCardByPrinting(cds, normalizedName, normalizedSet, normalizedCollector, out ocd, out var lookupError))
                {
                    skippedLines++;
                    errors.Add($"Line {csv.Context.Parser!.Row}: {lookupError}");
                    continue;
                }
            }

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

            var (purchasePrice, purchaseCurrency) = ResolvePurchasePrice(
                0,
                null,
                record.IsFoil,
                ocd,
                marketProvider,
                userCurrency);

            var backImageUrl = CardDataService.ResolveBackImageUrl(ocd);
            const CardLanguage language = CardLanguage.En;

            importedCards.Add(new Card
            {
                Name = ocd.Name,
                ScryfallId = ocd.Id,
                OracleId = CardDataService.ResolveOracleId(ocd) ?? string.Empty,
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
                IsAltered = false,
                CollectionId = collectionId,
                ArtCrop = artCrop,
                BackImageUrl = backImageUrl
            });
        }

        return new CollectionImportResult(importedCards, errors, skippedLines);
    }

    public static async Task<CollectionImportResult> ProcessArchidektCsvFile(
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

        while (await csv.ReadAsync())
        {
            ArchidektCsvRecordDto record;
            try
            {
                record = csv.GetRecord<ArchidektCsvRecordDto>();
            }
            catch (Exception ex)
            {
                skippedLines++;
                errors.Add($"Line {csv.Context.Parser!.Row}: {ex.Message}");
                continue;
            }

            var scryfallId = record.ScryfallId?.Trim();
            ScryfallCardDto? ocd;

            if (!string.IsNullOrWhiteSpace(scryfallId))
            {
                if (!cds.CardDataById.TryGetValue(scryfallId, out ocd))
                {
                    skippedLines++;
                    errors.Add($"Line {csv.Context.Parser!.Row}: Card with Scryfall ID '{scryfallId}' was not found.");
                    continue;
                }
            }
            else
            {
                var normalizedName = record.Name?.Trim() ?? string.Empty;
                var normalizedSet = record.EditionCode?.Trim() ?? string.Empty;
                var normalizedCollector = record.CollectorNumber?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(normalizedName) ||
                    string.IsNullOrWhiteSpace(normalizedSet) ||
                    string.IsNullOrWhiteSpace(normalizedCollector))
                {
                    skippedLines++;
                    errors.Add($"Line {csv.Context.Parser!.Row}: Missing required card data (Name, Edition Code, or Collector Number).");
                    continue;
                }

                if (!TryResolveCardByPrinting(cds, normalizedName, normalizedSet, normalizedCollector, out ocd, out var lookupError))
                {
                    skippedLines++;
                    errors.Add($"Line {csv.Context.Parser!.Row}: {lookupError}");
                    continue;
                }
            }

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
            var initialCurrency = purchasePriceInput > 0
                ? userCurrency
                : (Currency?)null;

            var (purchasePrice, purchaseCurrency) = ResolvePurchasePrice(
                purchasePriceInput,
                initialCurrency,
                record.IsFoil,
                ocd,
                marketProvider,
                userCurrency);

            var backImageUrl = CardDataService.ResolveBackImageUrl(ocd);
            var language = CardLanguageExtensions.ParseOrDefault(record.Language);

            importedCards.Add(new Card
            {
                Name = ocd.Name,
                ScryfallId = ocd.Id,
                OracleId = CardDataService.ResolveOracleId(ocd) ?? string.Empty,
                Quantity = record.Quantity,
                Language = language,
                Condition = Condition.NearMint,
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
                IsAltered = false,
                CollectionId = collectionId,
                ArtCrop = artCrop,
                BackImageUrl = backImageUrl
            });
        }

        return new CollectionImportResult(importedCards, errors, skippedLines);
    }

    public static async Task<CollectionImportResult> ProcessDragonshieldCsvFile(
        IFormFile file,
        CardDataService cds,
        int collectionId,
        MarketProvider marketProvider,
        Currency userCurrency)
    {
        await using var stream = file.OpenReadStream();
        using var streamReader = new StreamReader(stream);
        TextReader reader = streamReader;
        var firstLine = await streamReader.ReadLineAsync();
        var firstLineForCheck = firstLine?.TrimStart('\uFEFF', ' ', '\t');
        if (!string.IsNullOrWhiteSpace(firstLineForCheck) &&
            (firstLineForCheck.StartsWith("sep=", StringComparison.OrdinalIgnoreCase) ||
             firstLineForCheck.StartsWith("\"sep=", StringComparison.OrdinalIgnoreCase)))
        {
            // skip separator declaration
        }
        else if (firstLine != null)
        {
            var remainder = await streamReader.ReadToEndAsync();
            reader = new StringReader(firstLine + Environment.NewLine + remainder);
        }
        
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        var importedCards = new List<Card>();
        var errors = new List<string>();
        var skippedLines = 0;

        while (await csv.ReadAsync())
        {
            DragonshieldCsvRecordDto record;
            try
            {
                record = csv.GetRecord<DragonshieldCsvRecordDto>();
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
                errors.Add($"Line {csv.Context.Parser!.Row}: Missing required card data (Card Name, Set Code, or Card Number).");
                continue;
            }

            if (!TryResolveCardByPrinting(cds, normalizedName, normalizedSet, normalizedCollector, out var ocd, out var lookupError))
            {
                skippedLines++;
                errors.Add($"Line {csv.Context.Parser!.Row}: {lookupError}");
                continue;
            }

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
                null,
                record.IsFoil,
                ocd,
                marketProvider,
                userCurrency);

            var backImageUrl = CardDataService.ResolveBackImageUrl(ocd);
            var language = CardLanguageExtensions.ParseOrDefault(record.Language);

            importedCards.Add(new Card
            {
                Name = ocd.Name,
                ScryfallId = ocd.Id,
                OracleId = CardDataService.ResolveOracleId(ocd) ?? string.Empty,
                Quantity = record.Quantity,
                Language = language,
                Condition = Condition.NearMint,
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
                IsAltered = false,
                CollectionId = collectionId,
                ArtCrop = artCrop,
                BackImageUrl = backImageUrl
            });
        }

        return new CollectionImportResult(importedCards, errors, skippedLines);
    }

    public static async Task<CollectionImportResult> ProcessDelverCsvFile(
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

        while (await csv.ReadAsync())
        {
            DelverCsvRecordDto record;
            try
            {
                record = csv.GetRecord<DelverCsvRecordDto>();
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

            var purchasePriceInput = ParsePurchasePrice(record.Price);
            var currencyInput = CurrencyExtensions.ParseNullable(record.Currency);

            var (purchasePrice, purchaseCurrency) = ResolvePurchasePrice(
                purchasePriceInput,
                currencyInput,
                record.IsFoil,
                ocd,
                marketProvider,
                userCurrency);

            var backImageUrl = CardDataService.ResolveBackImageUrl(ocd);
            var language = CardLanguageExtensions.ParseOrDefault(record.Language);
            var cardCondition = ConvertCondition(record.Condition);

            importedCards.Add(new Card
            {
                Name = ocd.Name,
                ScryfallId = ocd.Id,
                OracleId = CardDataService.ResolveOracleId(ocd) ?? string.Empty,
                Quantity = record.Quantity,
                Language = language,
                Condition = cardCondition,
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
                IsAltered = false,
                CollectionId = collectionId,
                ArtCrop = artCrop,
                BackImageUrl = backImageUrl
            });
        }

        return new CollectionImportResult(importedCards, errors, skippedLines);
    }

    private static bool TryResolveCardByPrinting(
        CardDataService cds,
        string name,
        string setCode,
        string collectorNumber,
        [NotNullWhen(true)] out ScryfallCardDto? card,
        out string? errorMessage)
    {
        card = null;
        errorMessage = null;

        var matches = cds.CardDataById.Values
            .Where(c =>
                string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.Set, setCode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(c.CollectorNumber ?? string.Empty, collectorNumber, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();

        if (matches.Count != 1)
        {
            errorMessage = matches.Count == 0
                ? $"Card '{name}' with set '{setCode}' and collector number '{collectorNumber}' was not found."
                : $"Multiple cards matched '{name}' with set '{setCode}' and collector number '{collectorNumber}'.";
            return false;
        }

        card = matches[0];
        return true;
    }

    private static (double price, Currency currency) ResolvePurchasePrice(
        double purchasePrice,
        Currency? purchaseCurrency,
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
                currency = userCurrency;
            }
        }

        if (!currency.HasValue)
        {
            currency = userCurrency;
        }

        return (price, (Currency)currency);
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

    private static Condition ConvertCondition(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return Condition.NearMint;
        }

        var normalized = condition.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
        return Enum.TryParse<Condition>(normalized, true, out var parsed)
            ? parsed
            : Condition.NearMint;
    }
}
