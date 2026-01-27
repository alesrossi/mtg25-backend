using System.Globalization;
using API.Dtos.Decks;
using API.Logging;

namespace API.Services;

public class DecklistParserService : IDecklistParserService
{
    private readonly CardDataService _cardDataService;
    private readonly IValidationService _validationService;
    private readonly ILogger<DecklistParserService> _logger;
    private const string ParseOperation = "Decklist.Parse";

    public DecklistParserService(CardDataService cardDataService, IValidationService validationService, ILogger<DecklistParserService> logger)
    {
        _cardDataService = cardDataService;
        _validationService = validationService;
        _logger = logger;
    }

    public Task<DecklistParseResult> ParseAsync(IEnumerable<string> decklistLines)
    {
        var errors = new List<string>();
        var deckCards = new Dictionary<string, CreateDeckCardDto>(StringComparer.OrdinalIgnoreCase);

        if (decklistLines is null)
        {
            _logger.LogOperationWarning(ParseOperation, "Decklist null");
            return Task.FromResult(new DecklistParseResult(Array.Empty<CreateDeckCardDto>(), ["Decklist cannot be null."
            ]));
        }

        var decklistArray = decklistLines as string[] ?? decklistLines.ToArray();

        using var scope = _logger.BeginOperationScope(ParseOperation);
        _logger.LogOperationStart(ParseOperation, new { LineCount = decklistArray.Length });

        var inSideboard = false;
        var lineNumber = 0;

        foreach (var rawLine in decklistArray)
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
            }
        }

        var validDeckCards = new List<CreateDeckCardDto>();
        foreach (var deckCard in deckCards.Values)
        {
            var (isValid, validationErrors) = _validationService.ValidateModel(deckCard);
            if (!isValid)
            {
                _logger.LogOperationStep(ParseOperation, "Validation failed", new { deckCard.Name, validationErrors });
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

        _logger.LogOperationSuccess(ParseOperation, new { ValidCards = validDeckCards.Count, ErrorCount = errors.Count });
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
            _logger.LogOperationStep(ParseOperation, "Invalid format", new { lineNumber, line });
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
            _logger.LogOperationStep(ParseOperation, "Invalid quantity", new { lineNumber, Quantity = split[0] });
            return false;
        }

        var cardName = NormalizeCardName(split[1]);
        if (string.IsNullOrEmpty(cardName))
        {
            errors.Add($"Line {lineNumber}: Card name is required.");
            _logger.LogOperationStep(ParseOperation, "Missing card name", new { lineNumber });
            return false;
        }

        if (!_cardDataService.CardDataByName.TryGetValue(cardName, out var cardData))
        {
            errors.Add($"Line {lineNumber}: Card '{cardName}' was not found in the card database.");
            _logger.LogOperationStep(ParseOperation, "Card not found", new { lineNumber, cardName });
            return false;
        }

        var imageUris = CardDataService.ResolveImageUris(cardData);
        var imageUrl = imageUris?.Normal ?? imageUris?.Large ?? imageUris?.Png;
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            errors.Add($"Line {lineNumber}: Card '{cardName}' is missing image data.");
            _logger.LogOperationStep(ParseOperation, "Missing image", new { lineNumber, cardName });
            return false;
        }

        if (!deckCards.TryGetValue(cardData.Name, out var existingDto))
        {
            existingDto = new CreateDeckCardDto
            {
                Name = cardData.Name,
                MaindeckQuantity = 0,
                SideboardQuantity = 0
            };

            deckCards[cardData.Name] = existingDto;
        }

        if (inSideboard)
        {
            existingDto.SideboardQuantity += quantity;
        }
        else
        {
            existingDto.MaindeckQuantity += quantity;
        }

        _logger.LogOperationStep(ParseOperation, inSideboard ? "Added sideboard card" : "Added maindeck card", new { cardName, quantity });
        return true;
    }

    private static string NormalizeCardName(string rawName)
    {
        var trimmed = rawName.Trim();
        if (trimmed.Length == 0 || !trimmed.EndsWith(')'))
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
