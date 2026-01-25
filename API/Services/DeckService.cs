using System.Globalization;
using API.Dtos.Decks;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Core.Specifications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace API.Services;

public interface IDeckService
{
    Task<IReadOnlyList<DeckDto>> GetDecksForUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<DeckDto> GetDeckByIdAsync(int id, string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeckCardDto>> GetDeckCardsAsync(int deckId, string userId, bool maindeckOnly, bool sideboardOnly, bool? ownedOnly, CancellationToken cancellationToken = default);
    Task<DeckCardDto> GetDeckCardByIdAsync(int deckId, int id, string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeckCardDto>> GetMissingDeckCardsAsync(int deckId, string userId, CancellationToken cancellationToken = default);
    Task<string> ExportDeckAsync(int deckId, string userId, CancellationToken cancellationToken = default);
    Task<DeckDto> CreateDeckAsync(CreateDeckDto createDto, string userId, CancellationToken cancellationToken = default);
    Task<UpdateDeckResultDto> UpdateDeckAsync(int id, UpdateDeckDto updateDto, string userId, CancellationToken cancellationToken = default);
    Task DeleteDeckAsync(int id, string userId, CancellationToken cancellationToken = default);
    Task<DeckCardDto> CreateDeckCardAsync(int deckId, CreateDeckCardDto createDto, string userId, CancellationToken cancellationToken = default);
    Task<DeckCardDto> UpdateDeckCardAsync(int deckId, int id, UpdateDeckCardDto updateDto, string userId, CancellationToken cancellationToken = default);
    Task<DeckCardDto> UpdateDeckCardVersionAsync(int deckId, int id, UpdateDeckCardVersionDto updateDto, string userId, CancellationToken cancellationToken = default);
    Task DeleteDeckCardAsync(int deckId, int id, string userId, CancellationToken cancellationToken = default);
    Task<ImportDeckDto> ImportDeckFromDecklistAsync(DeckImportRequestDto importDto, string userId, CancellationToken cancellationToken = default);
}

public sealed class DeckService : IDeckService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<AppUser> _userManager;
    private readonly DeckCardService _deckCardService;
    private readonly IValidationService _validationService;
    private readonly IDecklistParserService _decklistParserService;
    private readonly CardDataService _cardDataService;
    private readonly IUserSettingsService _userSettingsService;
    private readonly IDeckHistoryService _deckHistoryService;

    public DeckService(
        IUnitOfWork unitOfWork,
        UserManager<AppUser> userManager,
        DeckCardService deckCardService,
        IValidationService validationService,
        IDecklistParserService decklistParserService,
        CardDataService cardDataService,
        IUserSettingsService userSettingsService,
        IDeckHistoryService deckHistoryService)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _deckCardService = deckCardService;
        _validationService = validationService;
        _decklistParserService = decklistParserService;
        _cardDataService = cardDataService;
        _userSettingsService = userSettingsService;
        _deckHistoryService = deckHistoryService;
    }

    public async Task<IReadOnlyList<DeckDto>> GetDecksForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw DeckServiceException.Unauthorized("Errors.Decks.MissingUserId");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            throw DeckServiceException.Unauthorized("Errors.Decks.UserNotFound");
        }

        var decks = await _unitOfWork.Repository<Deck>().ListAsync(new DecksWIthOwnerSpecification(user.Id), tracking: false);
        if (decks is null || decks.Count <= 0)
        {
            throw DeckServiceException.NotFound("Errors.Decks.NoneFound", includeBody: true, body: "Errors.Decks.NoneFound");
        }

        return decks.Select(MapToDto).ToList();
    }

    public async Task<DeckDto> GetDeckByIdAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw DeckServiceException.Unauthorized("Errors.Decks.MissingUserId");
        }

        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(id, tracking: false);
        if (deck == null || deck.OwnerId != userId)
        {
            throw DeckServiceException.NotFound("Errors.Decks.NotFoundOrUnauthorized");
        }

        return MapToDto(deck);
    }

    public async Task<IReadOnlyList<DeckCardDto>> GetDeckCardsAsync(
        int deckId,
        string userId,
        bool maindeckOnly,
        bool sideboardOnly,
        bool? ownedOnly,
        CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw DeckServiceException.Unauthorized("Errors.Decks.MissingUserId");
        }

        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(deckId, tracking: false);
        if (deck == null || deck.OwnerId != userId)
        {
            throw DeckServiceException.NotFound("Errors.Decks.NotFoundOrUnauthorized");
        }

        var deckCards = await _deckCardService.GetDeckCardsAsync(deckId, maindeckOnly, sideboardOnly, ownedOnly);
        return deckCards.ToList();
    }

    public async Task<DeckCardDto> GetDeckCardByIdAsync(int deckId, int id, string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw DeckServiceException.Unauthorized("Errors.Decks.MissingUserId");
        }

        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(deckId, tracking: false);
        if (deck == null || deck.OwnerId != userId)
        {
            throw DeckServiceException.NotFound("Errors.Decks.NotFoundOrUnauthorized");
        }

        var deckCard = await _deckCardService.GetDeckCardByIdAsync(id);
        if (deckCard == null || deckCard.DeckId != deckId)
        {
            throw DeckServiceException.NotFound("Errors.Decks.CardNotFound");
        }

        return deckCard;
    }

    public async Task<IReadOnlyList<DeckCardDto>> GetMissingDeckCardsAsync(int deckId, string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw DeckServiceException.Unauthorized("Errors.Decks.MissingUserId");
        }

        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(deckId, tracking: false);
        if (deck == null || deck.OwnerId != userId)
        {
            throw DeckServiceException.NotFound("Errors.Decks.NotFoundOrUnauthorized");
        }

        return (await _deckCardService.GetDeckCardsAsync(deckId, ownedOnly: false)).ToList();
    }

    public async Task<string> ExportDeckAsync(int deckId, string userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            throw DeckServiceException.Unauthorized("Errors.Decks.MissingUserId");
        }

        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(deckId, tracking: false);
        if (deck == null || deck.OwnerId != userId)
        {
            throw DeckServiceException.NotFound("Errors.Decks.NotFoundOrUnauthorized");
        }

        return deck.DeckList ?? string.Empty;
    }

    public async Task<DeckDto> CreateDeckAsync(CreateDeckDto createDto, string userId, CancellationToken cancellationToken = default)
    {
        if (userId == null)
        {
            throw DeckServiceException.Unauthorized("Errors.Decks.MissingUserId");
        }

        var deck = new Deck
        {
            Name = createDto.Name,
            Format = createDto.Format,
            OwnerId = userId,
            NumberOfCards = 0,
            TotalPrice = 0.0,
            DeckList = string.Empty
        };

        _unitOfWork.Repository<Deck>().Add(deck);
        await _unitOfWork.Complete();

        await _deckHistoryService.InitializeDeckHistoryAsync(deck.Id, userId, cancellationToken: cancellationToken);
        return MapToDto(deck);
    }

    public async Task<UpdateDeckResultDto> UpdateDeckAsync(int id, UpdateDeckDto updateDto, string userId, CancellationToken cancellationToken = default)
    {
        if (userId == null)
        {
            throw DeckServiceException.Unauthorized("Errors.Decks.MissingUserId");
        }

        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(id);
        if (deck == null || deck.OwnerId != userId)
        {
            throw DeckServiceException.NotFound("Errors.Decks.NotFoundOrUnauthorized");
        }

        if (updateDto.Name != null) deck.Name = updateDto.Name;
        if (updateDto.Format != null) deck.Format = updateDto.Format;
        deck.Image = updateDto.Image;

        var errors = Array.Empty<string>();
        var skippedLines = 0;
        if (updateDto.DeckList != null)
        {
            var result = await ReplaceDeckCardsFromDecklistAsync(deck, updateDto.DeckList, userId, allowPartial: true);
            errors = result.Errors.ToArray();
            skippedLines = result.SkippedLines;
        }

        _unitOfWork.Repository<Deck>().Update(deck);
        await _unitOfWork.Complete();

        return new UpdateDeckResultDto(MapToDto(deck), errors, skippedLines);
    }

    public async Task DeleteDeckAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        if (userId == null)
        {
            throw DeckServiceException.Unauthorized("Errors.Decks.MissingUserId");
        }

        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(id);
        if (deck == null || deck.OwnerId != userId)
        {
            throw DeckServiceException.NotFound("Errors.Decks.NotFoundOrUnauthorized");
        }

        var deckCardsSpec = new DeckCardsWithDeckIdSpecification(id);
        var deckCards = await _unitOfWork.Repository<DeckCard>().ListAsync(deckCardsSpec);
        if (deckCards?.Any() == true)
        {
            foreach (var deckCard in deckCards)
            {
                _unitOfWork.Repository<DeckCard>().Delete(deckCard);
            }
        }

        _unitOfWork.Repository<Deck>().Delete(deck);
        await _unitOfWork.Complete();
    }

    public async Task<DeckCardDto> CreateDeckCardAsync(int deckId, CreateDeckCardDto createDto, string userId, CancellationToken cancellationToken = default)
    {
        if (userId == null)
        {
            throw DeckServiceException.Unauthorized("Errors.Decks.MissingUserId");
        }

        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null)
        {
            throw DeckServiceException.NotFound("Errors.Decks.NotFound");
        }

        if (deck.OwnerId != userId)
        {
            throw DeckServiceException.Unauthorized("Errors.Decks.Unauthorized");
        }

        var (isValid, errors) = _validationService.ValidateModel(createDto);
        if (!isValid || createDto.MaindeckQuantity + createDto.SideboardQuantity == 0)
        {
            throw DeckServiceException.BadRequest("Errors.Decks.ValidationFailed", new { errors }, includeBody: true);
        }

        try
        {
            return await _deckCardService.CreateDeckCardAsync(deckId, createDto);
        }
        catch (InvalidOperationException ex)
        {
            throw DeckServiceException.BadRequest(ex.Message, new { errors = new[] { ex.Message } }, includeBody: true);
        }
    }

    public async Task<DeckCardDto> UpdateDeckCardAsync(int deckId, int id, UpdateDeckCardDto updateDto, string userId, CancellationToken cancellationToken = default)
    {
        if (userId == null)
        {
            throw DeckServiceException.Unauthorized("Errors.Decks.MissingUserId");
        }

        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null || deck.OwnerId != userId)
        {
            throw DeckServiceException.NotFound("Errors.Decks.NotFoundOrUnauthorized");
        }

        var existingDeckCard = await _deckCardService.GetDeckCardByIdAsync(id);
        if (existingDeckCard == null || existingDeckCard.DeckId != deckId)
        {
            throw DeckServiceException.NotFound("Errors.Decks.CardNotFoundOrMismatched");
        }

        try
        {
            return await _deckCardService.UpdateDeckCardAsync(id, updateDto);
        }
        catch (InvalidOperationException ex)
        {
            throw DeckServiceException.BadRequest(ex.Message, new { errors = new[] { ex.Message } }, includeBody: true);
        }
    }

    public async Task<DeckCardDto> UpdateDeckCardVersionAsync(int deckId, int id, UpdateDeckCardVersionDto updateDto, string userId, CancellationToken cancellationToken = default)
    {
        if (userId == null)
        {
            throw DeckServiceException.Unauthorized("Errors.Decks.MissingUserId");
        }

        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null || deck.OwnerId != userId)
        {
            throw DeckServiceException.NotFound("Errors.Decks.NotFoundOrUnauthorized");
        }

        var deckCard = await _unitOfWork.Repository<DeckCard>().GetByIdAsync(id);
        if (deckCard == null || deckCard.DeckId != deckId)
        {
            throw DeckServiceException.NotFound("Errors.Decks.CardNotFoundOrMismatched");
        }

        if (!_cardDataService.CardDataById.TryGetValue(updateDto.ScryfallId, out var scryfallCard))
        {
            throw DeckServiceException.BadRequest("Errors.Decks.InvalidScryfallId", new { errors = new[] { "Invalid Scryfall ID provided." } }, includeBody: true);
        }

        if (!string.Equals(scryfallCard.Name, deckCard.Name, StringComparison.OrdinalIgnoreCase))
        {
            throw DeckServiceException.BadRequest("Errors.Decks.ScryfallNameMismatch", new { errors = new[] { $"'{updateDto.ScryfallId}' is not a valid version for '{deckCard.Name}'." } }, includeBody: true);
        }

        try
        {
            var updatedDeckCard = (await _deckCardService.UpdateDeckCardVersionAsync(id, updateDto, scryfallCard))!;

            _unitOfWork.Repository<DeckCard>().Update(deckCard);
            await _unitOfWork.Complete();

            return updatedDeckCard;
        }
        catch (InvalidOperationException ex)
        {
            throw DeckServiceException.BadRequest(ex.Message, new { errors = new[] { ex.Message } }, includeBody: true);
        }
        catch (Exception ex)
        {
            throw DeckServiceException.Problem(
                "Deck card update failed",
                "Errors.Decks.CardUpdateFailed",
                StatusCodes.Status500InternalServerError,
                ex);
        }
    }

    public async Task DeleteDeckCardAsync(int deckId, int id, string userId, CancellationToken cancellationToken = default)
    {
        if (userId == null)
        {
            throw DeckServiceException.Unauthorized("Errors.Decks.MissingUserId");
        }

        var deck = await _unitOfWork.Repository<Deck>().GetByIdAsync(deckId);
        if (deck == null || deck.OwnerId != userId)
        {
            throw DeckServiceException.NotFound("Errors.Decks.NotFoundOrUnauthorized");
        }

        var existingDeckCard = await _deckCardService.GetDeckCardByIdAsync(id);
        if (existingDeckCard == null || existingDeckCard.DeckId != deckId)
        {
            throw DeckServiceException.NotFound("Errors.Decks.CardNotFoundOrMismatched");
        }

        var deleted = await _deckCardService.DeleteDeckCardAsync(id);
        if (!deleted)
        {
            throw DeckServiceException.NotFound("Errors.Decks.CardDeleteFailed");
        }
    }

    public async Task<ImportDeckDto> ImportDeckFromDecklistAsync(DeckImportRequestDto importDto, string userId, CancellationToken cancellationToken = default)
    {
        if (userId == null)
        {
            throw DeckServiceException.Unauthorized("Errors.Decks.MissingUserId");
        }

        var (isValid, validationErrors) = _validationService.ValidateModel(importDto);
        if (!isValid)
        {
            throw DeckServiceException.BadRequest("Errors.Decks.ImportInvalidPayload", new { errors = validationErrors }, includeBody: true);
        }

        var parseResult = await ParseDecklistAsync(importDto.Decklist, allowPartial: true);

        var deck = new Deck
        {
            Name = importDto.Name.Trim(),
            Format = importDto.Format.Trim(),
            OwnerId = userId,
            NumberOfCards = 0,
            TotalPrice = 0,
            TotalPriceCurrency = null,
            DeckList = string.Empty
        };

        _unitOfWork.Repository<Deck>().Add(deck);
        await _unitOfWork.Complete();

        var result = await ReplaceDeckCardsFromDecklistAsync(deck, importDto.Decklist, userId, allowPartial: true);
        var createdCards = result.CreatedCards;

        await _deckHistoryService.InitializeDeckHistoryAsync(deck.Id, userId, cancellationToken: cancellationToken);
        return new ImportDeckDto(MapToDto(deck), createdCards, result.Errors, result.SkippedLines);
    }

    private async Task<(IReadOnlyList<CreateDeckCardDto> DeckCards, IReadOnlyList<string> Errors, int SkippedLines)> ParseDecklistAsync(
        string decklist,
        bool allowPartial)
    {
        var decklistLines = FilterDecklistLines(decklist);
        var parseResult = await _decklistParserService.ParseAsync(decklistLines);
        var skippedLines = parseResult.Errors.Count;

        if (!parseResult.DeckCards.Any())
        {
            var errors = parseResult.Errors.Any()
                ? parseResult.Errors
                : new[] { "Decklist did not contain any valid cards." };

            throw DeckServiceException.BadRequest("Errors.Decks.NoCardsParsed", new
            {
                errors,
                deckCards = parseResult.DeckCards,
                skippedLines
            }, includeBody: true);
        }

        if (!allowPartial && parseResult.Errors.Any())
        {
            throw DeckServiceException.BadRequest("Errors.Decks.ImportInvalidPayload", new
            {
                errors = parseResult.Errors,
                deckCards = parseResult.DeckCards,
                skippedLines
            }, includeBody: true);
        }

        return (parseResult.DeckCards, parseResult.Errors, skippedLines);
    }

    private async Task<(List<DeckCardDto> CreatedCards, IReadOnlyList<string> Errors, int SkippedLines)> ReplaceDeckCardsFromDecklistAsync(
        Deck deck,
        string decklist,
        string userId,
        bool allowPartial)
    {
        var parseResult = await ParseDecklistAsync(decklist, allowPartial);

        var existingCards = await _unitOfWork.Repository<DeckCard>()
            .ListAsync(new DeckCardsWithDeckIdSpecification(deck.Id));
        if (existingCards != null)
        {
            foreach (var existing in existingCards)
            {
                _unitOfWork.Repository<DeckCard>().Delete(existing);
            }
            await _unitOfWork.Complete();
        }

        deck.NumberOfCards = 0;
        deck.NumberOfMainBoardCards = 0;
        deck.NumberOfSideBoardCards = 0;
        deck.TotalPrice = 0;
        deck.TotalPriceCurrency = null;
        deck.DeckList = string.Empty;
        deck.ColorIdentity = [];
        _unitOfWork.Repository<Deck>().Update(deck);
        await _unitOfWork.Complete();

        var createdCards = new List<DeckCardDto>();
        var marketProvider = await _userSettingsService.GetMarketProviderAsync(userId);
        deck.TotalPriceCurrency = _userSettingsService.ResolveCurrency(marketProvider);
        var totalPrice = 0.0;

        foreach (var deckCardDto in parseResult.DeckCards)
        {
            var created = await _deckCardService.CreateDeckCardAsync(deck.Id, deckCardDto);
            deck.ColorIdentity.AddRange(created.ColorIdentity.Except(deck.ColorIdentity));
            createdCards.Add(created);

            var cardPrice = ResolveCardMarketPrice(_cardDataService, created.ScryfallId, marketProvider);
            var quantity = created.MaindeckQuantity + created.SideboardQuantity;
            totalPrice += cardPrice * quantity;
        }

        deck.NumberOfCards = createdCards.Sum(dc => dc.MaindeckQuantity + dc.SideboardQuantity);
        deck.NumberOfMainBoardCards = createdCards.Sum(dc => dc.MaindeckQuantity);
        deck.NumberOfSideBoardCards = createdCards.Sum(dc => dc.SideboardQuantity);
        deck.TotalPrice = Math.Round(totalPrice, 2, MidpointRounding.AwayFromZero);
        deck.DeckList = BuildDeckList(createdCards);
        _unitOfWork.Repository<Deck>().Update(deck);
        await _unitOfWork.Complete();

        return (createdCards, parseResult.Errors, parseResult.SkippedLines);
    }

    private static string BuildDeckList(IEnumerable<DeckCardDto> deckCards)
    {
        var maindeckLines = deckCards
            .Where(card => card.MaindeckQuantity > 0)
            .OrderBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
            .Select(card => $"{card.MaindeckQuantity} {card.Name}")
            .ToList();

        var sideboardLines = deckCards
            .Where(card => card.SideboardQuantity > 0)
            .OrderBy(card => card.Name, StringComparer.OrdinalIgnoreCase)
            .Select(card => $"{card.SideboardQuantity} {card.Name}")
            .ToList();

        if (maindeckLines.Count == 0 && sideboardLines.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>(maindeckLines);
        if (sideboardLines.Count > 0)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.AddRange(sideboardLines);
        }

        return string.Join('\n', lines);
    }

    private static DeckDto MapToDto(Deck deck)
    {
        return new DeckDto
        {
            Id = deck.Id,
            Name = deck.Name,
            Format = deck.Format,
            Image = deck.Image,
            NumberOfCards = deck.NumberOfCards,
            NumberOfMainBoardCards = deck.NumberOfMainBoardCards,
            NumberOfSideBoardCards = deck.NumberOfSideBoardCards,
            TotalPrice = deck.TotalPrice,
            TotalPriceCurrency = deck.TotalPriceCurrency,
            ColorIdentity = deck.ColorIdentity.ToList(),
            OwnerId = deck.OwnerId
        };
    }

    private static string[] FilterDecklistLines(string decklist)
    {
        var rawLines = decklist
            .Replace("\r", string.Empty)
            .Split('\n');

        var filteredLines = new List<string>();
        var dividerAdded = false;

        foreach (var rawLine in rawLines)
        {
            var trimmed = rawLine.Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                if (!dividerAdded)
                {
                    filteredLines.Add(string.Empty);
                    dividerAdded = true;
                }
                continue;
            }

            if (char.IsDigit(trimmed[0]))
            {
                filteredLines.Add(trimmed);
            }
        }

        return filteredLines.ToArray();
    }

    private static double ResolveCardMarketPrice(CardDataService cardDataService, string scryfallId, MarketProvider provider)
    {
        if (!cardDataService.CardDataById.TryGetValue(scryfallId, out var cardData) || cardData.Prices is null)
        {
            return 0;
        }

        var priceText = provider == MarketProvider.Mkm
            ? cardData.Prices.Eur
            : cardData.Prices.Usd;

        if (string.IsNullOrWhiteSpace(priceText))
        {
            return 0;
        }

        return double.TryParse(priceText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }
}

public sealed class DeckServiceException : Exception
{
    public int StatusCode { get; }
    public object? Body { get; }
    public bool IncludeBody { get; }
    public string? ProblemTitle { get; }
    public string? ProblemDetail { get; }

    private DeckServiceException(int statusCode, string? message, object? body, bool includeBody, string? problemTitle = null, string? problemDetail = null, Exception? innerException = null)
        : base(message ?? string.Empty, innerException)
    {
        StatusCode = statusCode;
        Body = body;
        IncludeBody = includeBody;
        ProblemTitle = problemTitle;
        ProblemDetail = problemDetail;
    }

    public static DeckServiceException Unauthorized(string message)
        => new(StatusCodes.Status401Unauthorized, message, null, false);

    public static DeckServiceException NotFound(string message)
        => new(StatusCodes.Status404NotFound, message, null, false);

    public static DeckServiceException NotFound(string message, bool includeBody, object? body)
        => new(StatusCodes.Status404NotFound, message, body, includeBody);

    public static DeckServiceException BadRequest(string message, object? body, bool includeBody)
        => new(StatusCodes.Status400BadRequest, message, body, includeBody);

    public static DeckServiceException Problem(string title, string detail, int statusCode, Exception? innerException = null)
        => new(statusCode, detail, null, false, title, detail, innerException);
}
