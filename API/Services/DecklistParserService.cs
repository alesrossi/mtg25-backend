using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using API.Dtos.Decks;

namespace API.Services;

public class DecklistParserService(CardDataService cardDataService, IValidationService validationService) : IDecklistParserService
{
    public Task<DecklistParseResult> ParseAsync(IEnumerable<string> decklistLines)
    {
        var errors = new List<string>();
        var deckCards = new Dictionary<string, CreateDeckCardDto>();

        if (decklistLines is null)
        {
            return Task.FromResult(new DecklistParseResult(Array.Empty<CreateDeckCardDto>(), new[] { "Decklist cannot be null." }));
        }

        var inSideboard = false;
        var lineNumber = 0;

        foreach (var rawLine in decklistLines)
        {
            lineNumber++;
            var line = rawLine?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(line))
            {
                if (!inSideboard)
                {
                    inSideboard = true;
                }
                continue;
            }

            if (!TryParseLine(line, lineNumber, inSideboard, deckCards, errors))
            {
                continue;
            }
        }

        var validDeckCards = new List<CreateDeckCardDto>();
        foreach (var deckCard in deckCards.Values)
        {
            var (isValid, validationErrors) = validationService.ValidateModel(deckCard);
            if (!isValid)
            {
                foreach (var (_, messages) in validationErrors)
                {
                    errors.AddRange(messages);
                }
                continue;
            }

            if (deckCard.MaindeckQuantity + deckCard.SideboardQuantity <= 0)
            {
                errors.Add($"Card '{deckCard.Name}' must have a quantity greater than zero.");
                continue;
            }

            validDeckCards.Add(deckCard);
        }

        return Task.FromResult(new DecklistParseResult(validDeckCards, errors));
    }

    private bool TryParseLine(
        string line,
        int lineNumber,
        bool inSideboard,
        IDictionary<string, CreateDeckCardDto> deckCards,
        ICollection<string> errors)
    {
        var split = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (split.Length < 2)
        {
            errors.Add($"Line {lineNumber}: Invalid format. Expected '<quantity> <card name>'.");
            return false;
        }

        var quantityToken = split[0];
        if (quantityToken.EndsWith("x", StringComparison.OrdinalIgnoreCase))
        {
            quantityToken = quantityToken[..^1];
        }

        if (!int.TryParse(quantityToken, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
        {
            errors.Add($"Line {lineNumber}: Quantity '{split[0]}' is not a positive integer.");
            return false;
        }

        var cardName = NormalizeCardName(split[1]);
        if (string.IsNullOrEmpty(cardName))
        {
            errors.Add($"Line {lineNumber}: Card name is required.");
            return false;
        }

        if (!cardDataService.CardDataByName.TryGetValue(cardName, out var cardData))
        {
            errors.Add($"Line {lineNumber}: Card '{cardName}' was not found in the card database.");
            return false;
        }

        var imageUris = cardDataService.ResolveImageUris(cardData);
        var imageUrl = imageUris?.Normal ?? imageUris?.Large ?? imageUris?.Png;
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            errors.Add($"Line {lineNumber}: Card '{cardName}' is missing image data.");
            return false;
        }

        if (!deckCards.TryGetValue(cardData.OracleId, out var existingDto))
        {
            existingDto = new CreateDeckCardDto
            {
                OracleId = cardData.Id,
                Name = cardData.Name,
                SetCode = cardData.Set,
                SetName = cardData.SetName,
                ImageUrl = imageUrl,
                Rarity = cardData.Rarity,
                CollectorNumber = cardData.CollectorNumber,
                MaindeckQuantity = 0,
                SideboardQuantity = 0
            };

            deckCards[cardData.OracleId] = existingDto;
        }

        if (inSideboard)
        {
            existingDto.SideboardQuantity += quantity;
        }
        else
        {
            existingDto.MaindeckQuantity += quantity;
        }

        return true;
    }

    private static string NormalizeCardName(string rawName)
    {
        var trimmed = rawName.Trim();
        if (trimmed.Length == 0 || !trimmed.EndsWith(")", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var openParenIndex = trimmed.LastIndexOf('(');
        if (openParenIndex <= 0)
        {
            return trimmed;
        }

        var baseName = trimmed[..openParenIndex].Trim();
        return baseName.Length == 0 ? trimmed : baseName;
    }
}
