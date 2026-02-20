using API.Dtos.Wishlists;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;
using Core.Enums;

namespace API.Services;

public interface IWishlistService
{
    Task<IReadOnlyList<WishlistSummaryDto>> GetWishlistsAsync(string userId, CancellationToken cancellationToken = default);
    Task<WishlistDto> GetWishlistByIdAsync(int id, string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WishlistCardDto>> GetWishlistCardsAsync(int wishlistId, string userId, CancellationToken cancellationToken = default);
    Task<WishlistCardDto> GetWishlistCardByIdAsync(int wishlistId, int cardId, string userId, CancellationToken cancellationToken = default);
    Task<WishlistDto> CreateWishlistAsync(CreateWishlistDto createDto, string userId, CancellationToken cancellationToken = default);
    Task<WishlistDto> UpdateWishlistAsync(int id, UpdateWishlistDto updateDto, string userId, CancellationToken cancellationToken = default);
    Task DeleteWishlistAsync(int id, string userId, CancellationToken cancellationToken = default);
    Task<List<WishlistCard>> CreateWishlistCardsAsync(int wishlistId, List<CreateWishlistCardDto> newWishlistCardList, string userId, CancellationToken cancellationToken = default);
    Task<WishlistCardDto> UpdateWishlistCardAsync(int wishlistId, int cardId, UpdateWishlistCardDto updateDto, string userId, CancellationToken cancellationToken = default);
    Task DeleteWishlistCardAsync(int wishlistId, int cardId, string userId, CancellationToken cancellationToken = default);
}

public sealed class WishlistService : IWishlistService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidationService _validationService;
    private readonly CardDataService _cardDataService;
    private readonly WishlistPricingService _wishlistPricingService;

    public WishlistService(
        IUnitOfWork unitOfWork,
        IValidationService validationService,
        CardDataService cardDataService,
        WishlistPricingService wishlistPricingService)
    {
        _unitOfWork = unitOfWork;
        _validationService = validationService;
        _cardDataService = cardDataService;
        _wishlistPricingService = wishlistPricingService;
    }

    public async Task<IReadOnlyList<WishlistSummaryDto>> GetWishlistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw WishlistServiceException.Unauthorized("Errors.Wishlists.MissingUserId");
        }

        var spec = new WishlistsWithOwnerSpecification(userId, includeCards: true);
        var wishlists = await _unitOfWork.Repository<Wishlist>().ListAsync(spec, tracking: false) ?? Array.Empty<Wishlist>();

        return wishlists.Select(MapToSummaryDto).ToList();
    }

    public async Task<WishlistDto> GetWishlistByIdAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        var spec = new WishlistByIdWithCardsSpecification(id);
        var wishlist = await _unitOfWork.Repository<Wishlist>().GetEntityWithSpec(spec, tracking: false);
        if (wishlist == null)
        {
            throw WishlistServiceException.NotFound("Errors.Wishlists.NotFound");
        }

        var isOwner = !string.IsNullOrEmpty(userId) && wishlist.OwnerId == userId;
        if (!isOwner && !wishlist.IsPublic)
        {
            throw WishlistServiceException.Unauthorized("Errors.Wishlists.Unauthorized");
        }

        return MapToDto(wishlist);
    }

    public async Task<IReadOnlyList<WishlistCardDto>> GetWishlistCardsAsync(int wishlistId, string userId, CancellationToken cancellationToken = default)
    {
        var wishlist = await _unitOfWork.Repository<Wishlist>().GetByIdAsync(wishlistId, tracking: false);
        if (wishlist == null)
        {
            throw WishlistServiceException.NotFound("Errors.Wishlists.NotFound");
        }

        var isOwner = !string.IsNullOrEmpty(userId) && wishlist.OwnerId == userId;
        if (!isOwner && !wishlist.IsPublic)
        {
            throw WishlistServiceException.Unauthorized("Errors.Wishlists.Unauthorized");
        }

        var spec = new WishlistCardsWithWishlistIdSpecification(wishlistId);
        var cards = await _unitOfWork.Repository<WishlistCard>().ListAsync(spec, tracking: false) ?? Array.Empty<WishlistCard>();

        return cards.Select(MapToDto).ToList();
    }

    public async Task<WishlistCardDto> GetWishlistCardByIdAsync(int wishlistId, int cardId, string userId, CancellationToken cancellationToken = default)
    {
        await EnsureWishlistOwnershipAsync(wishlistId, userId, tracking: false);

        var wishlistCard = await _unitOfWork.Repository<WishlistCard>().GetByIdAsync(cardId, tracking: false);
        if (wishlistCard == null)
        {
            throw WishlistServiceException.NotFound("Errors.Wishlists.CardNotFound");
        }

        return MapToDto(wishlistCard);
    }

    public async Task<WishlistDto> CreateWishlistAsync(CreateWishlistDto createDto, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw WishlistServiceException.Unauthorized("Errors.Wishlists.MissingUserId");
        }

        var (isValid, errors) = _validationService.ValidateModel(createDto);
        if (!isValid)
        {
            throw WishlistServiceException.ValidationFailed(errors, "Errors.Wishlists.ValidationFailed");
        }

        var wishlist = new Wishlist
        {
            Name = createDto.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(createDto.Description) ? null : createDto.Description.Trim(),
            IsPublic = createDto.IsPublic,
            OwnerId = userId
        };

        _unitOfWork.Repository<Wishlist>().Add(wishlist);
        await _unitOfWork.Complete();

        return MapToDto(wishlist);
    }

    public async Task<WishlistDto> UpdateWishlistAsync(int id, UpdateWishlistDto updateDto, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw WishlistServiceException.Unauthorized("Errors.Wishlists.MissingUserId");
        }

        var (isValid, errors) = _validationService.ValidateModel(updateDto);
        if (!isValid)
        {
            throw WishlistServiceException.ValidationFailed(errors, "Errors.Wishlists.ValidationFailed");
        }

        var wishlist = await _unitOfWork.Repository<Wishlist>().GetByIdAsync(id);
        if (wishlist == null)
        {
            throw WishlistServiceException.NotFound("Errors.Wishlists.NotFound");
        }
        if (wishlist.OwnerId != userId)
        {
            throw WishlistServiceException.Unauthorized("Errors.Wishlists.Unauthorized");
        }

        wishlist.Name = updateDto.Name.Trim();
        wishlist.Description = string.IsNullOrWhiteSpace(updateDto.Description) ? null : updateDto.Description.Trim();
        wishlist.IsPublic = updateDto.IsPublic;

        _unitOfWork.Repository<Wishlist>().Update(wishlist);
        await _unitOfWork.Complete();

        var spec = new WishlistWithCardsSpecification(id, userId);
        var updated = await _unitOfWork.Repository<Wishlist>().GetEntityWithSpec(spec) ?? wishlist;

        return MapToDto(updated);
    }

    public async Task DeleteWishlistAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw WishlistServiceException.Unauthorized("Errors.Wishlists.MissingUserId");
        }

        var wishlist = await _unitOfWork.Repository<Wishlist>().GetByIdAsync(id);
        if (wishlist == null)
        {
            throw WishlistServiceException.NotFound("Errors.Wishlists.NotFound");
        }
        if (wishlist.OwnerId != userId)
        {
            throw WishlistServiceException.Unauthorized("Errors.Wishlists.Unauthorized");
        }

        _unitOfWork.Repository<Wishlist>().Delete(wishlist);
        await _unitOfWork.Complete();
    }

    public async Task<List<WishlistCard>> CreateWishlistCardsAsync(
        int wishlistId,
        List<CreateWishlistCardDto> newWishlistCardList,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureWishlistOwnershipAsync(wishlistId, userId);

        var (isValid, errors) = _validationService.ValidateModel(newWishlistCardList);
        if (!isValid)
        {
            throw WishlistServiceException.ValidationFailed(errors, "Errors.Wishlists.ValidationFailed");
        }

        var cardList = newWishlistCardList
            .Where(x => _cardDataService.CardDataById.ContainsKey(x.ScryfallId))
            .Select(x =>
            {
                var card = _cardDataService.CardDataById[x.ScryfallId];
                var imageUris = CardDataService.ResolveImageUris(card);
                var imageUrl = imageUris?.Large ?? imageUris?.Normal ?? imageUris?.Png ?? imageUris?.Small;
                var artCrop = imageUris!.ArtCrop;
                var backImageUrl = CardDataService.ResolveBackImageUrl(card);
                var oracleId = CardDataService.ResolveOracleId(card) ?? string.Empty;
                return new WishlistCard
                {
                    WishlistId = wishlistId,
                    DesiredQuantity = x.DesiredQuantity,
                    IsFoil = x.IsFoil,
                    Language = x.Language,
                    MinimumCondition = x.MinimumCondition,
                    Name = card.Name,
                    ScryfallId = x.ScryfallId,
                    OracleId = oracleId,
                    ExactVersion = x.ExactVersion,
                    Notes = x.Notes,
                    OriginalDeckId = x.OriginalDeckId,
                    ImageUrl = imageUrl,
                    BackImageUrl = backImageUrl,
                    ArtCrop = artCrop
                };
            }).ToList();

        _unitOfWork.Repository<WishlistCard>().Add(cardList);
        await _unitOfWork.Complete();

        await _wishlistPricingService.RecalculateTotalsAsync(wishlistId);

        return cardList;
    }

    public async Task<WishlistCardDto> UpdateWishlistCardAsync(
        int wishlistId,
        int cardId,
        UpdateWishlistCardDto updateDto,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureWishlistOwnershipAsync(wishlistId, userId);

        var (isValid, errors) = _validationService.ValidateModel(updateDto);
        if (!isValid)
        {
            throw WishlistServiceException.ValidationFailed(errors, "Errors.Wishlists.ValidationFailed");
        }

        var wishlistCard = await _unitOfWork.Repository<WishlistCard>().GetByIdAsync(cardId);
        if (wishlistCard == null || wishlistCard.WishlistId != wishlistId)
        {
            throw WishlistServiceException.NotFound("Errors.Wishlists.CardNotFound");
        }

        if (updateDto.ScryfallId is not null)
        {
            var card = _cardDataService.CardDataById[updateDto.ScryfallId];
            var oracleId = CardDataService.ResolveOracleId(card) ?? string.Empty;
            if (string.Equals(oracleId, wishlistCard.OracleId, StringComparison.OrdinalIgnoreCase))
            {
                var imageUris = CardDataService.ResolveImageUris(card);
                var imageUrl = imageUris.Large ?? imageUris?.Normal ?? imageUris?.Png ?? imageUris?.Small;
                var artCrop = imageUris!.ArtCrop;
                var backImageUrl = CardDataService.ResolveBackImageUrl(card);

                wishlistCard.ScryfallId = updateDto.ScryfallId;
                wishlistCard.OracleId = oracleId;
                wishlistCard.ImageUrl = imageUrl;
                wishlistCard.BackImageUrl = backImageUrl;
                wishlistCard.ArtCrop = artCrop;
            }
            else
            {
                throw WishlistServiceException.BadRequest("Errors.Wishlists.InvalidVersion", includeBody: true);
            }
        }
        if (updateDto.DesiredQuantity is not null) wishlistCard.DesiredQuantity = (int)updateDto.DesiredQuantity;
        if (updateDto.IsFoil is not null) wishlistCard.IsFoil = updateDto.IsFoil;
        if (updateDto.Language is not null) wishlistCard.Language = updateDto.Language;
        if (updateDto.ExactVersion is not null) wishlistCard.ExactVersion = (bool)updateDto.ExactVersion;
        if (updateDto.MinimumCondition is not null) wishlistCard.MinimumCondition = updateDto.MinimumCondition;
        if (updateDto.Notes is not null) wishlistCard.Notes = updateDto.Notes?.Trim() ?? string.Empty;

        _unitOfWork.Repository<WishlistCard>().Update(wishlistCard);
        await _unitOfWork.Complete();

        await _wishlistPricingService.RecalculateTotalsAsync(wishlistId);

        return MapToDto(wishlistCard);
    }

    public async Task DeleteWishlistCardAsync(int wishlistId, int cardId, string userId, CancellationToken cancellationToken = default)
    {
        await EnsureWishlistOwnershipAsync(wishlistId, userId);

        var wishlistCard = await _unitOfWork.Repository<WishlistCard>().GetByIdAsync(cardId);
        if (wishlistCard == null || wishlistCard.WishlistId != wishlistId)
        {
            throw WishlistServiceException.NotFound("Errors.Wishlists.CardNotFound");
        }

        _unitOfWork.Repository<WishlistCard>().Delete(wishlistCard);
        await _unitOfWork.Complete();

        await _wishlistPricingService.RecalculateTotalsAsync(wishlistId);
    }

    private async Task<Wishlist> EnsureWishlistOwnershipAsync(int wishlistId, string userId, bool tracking = true)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw WishlistServiceException.Unauthorized("Errors.Wishlists.MissingUserId");
        }

        var wishlist = await _unitOfWork.Repository<Wishlist>().GetByIdAsync(wishlistId, tracking);
        if (wishlist == null)
        {
            throw WishlistServiceException.NotFound("Errors.Wishlists.NotFound");
        }
        if (wishlist.OwnerId != userId)
        {
            throw WishlistServiceException.Unauthorized("Errors.Wishlists.Unauthorized");
        }

        return wishlist;
    }

    private static WishlistSummaryDto MapToSummaryDto(Wishlist wishlist)
    {
        var cards = wishlist.WishlistCards ?? new List<WishlistCard>();

        return new WishlistSummaryDto
        {
            Id = wishlist.Id,
            Name = wishlist.Name,
            Description = wishlist.Description,
            IsPublic = wishlist.IsPublic,
            TotalPrice = wishlist.TotalPrice,
            TotalPriceCurrency = wishlist.TotalPriceCurrency,
            CardsCount = cards.Sum(c => c.DesiredQuantity),
            IndividualCardsCount = cards.Count
        };
    }

    private static WishlistDto MapToDto(Wishlist wishlist)
    {
        var cards = wishlist.WishlistCards?.Select(MapToDto).ToList() ?? new List<WishlistCardDto>();

        return new WishlistDto
        {
            Id = wishlist.Id,
            Name = wishlist.Name,
            Description = wishlist.Description,
            IsPublic = wishlist.IsPublic,
            OwnerId = wishlist.OwnerId,
            TotalPrice = wishlist.TotalPrice,
            TotalPriceCurrency = wishlist.TotalPriceCurrency,
            CardsCount = cards.Sum(c => c.DesiredQuantity),
            IndividualCardsCount = cards.Count,
            Cards = cards
        };
    }

    private static WishlistCardDto MapToDto(WishlistCard card)
    {
        return new WishlistCardDto
        {
            Id = card.Id,
            WishlistId = card.WishlistId,
            ScryfallId = card.ScryfallId,
            ExactVersion = card.ExactVersion,
            Name = card.Name,
            ImageUrl = card.ImageUrl,
            BackImageUrl = card.BackImageUrl,
            DesiredQuantity = card.DesiredQuantity,
            IsFoil = card.IsFoil ?? false,
            Language = card.Language,
            MinimumCondition = card.MinimumCondition,
            Notes = card.Notes,
            OriginalDeckId = card.OriginalDeckId,
            IsAny = card.IsFoil is null && card.Language is null && card.MinimumCondition is null && !card.ExactVersion
        };
    }
}

public sealed class WishlistServiceException : Exception
{
    public int StatusCode { get; }
    public object? Body { get; }
    public bool IncludeBody { get; }

    private WishlistServiceException(int statusCode, string? message, object? body, bool includeBody)
        : base(message ?? string.Empty)
    {
        StatusCode = statusCode;
        Body = body;
        IncludeBody = includeBody;
    }

    public static WishlistServiceException Unauthorized(string message)
        => new(StatusCodes.Status401Unauthorized, message, null, false);

    public static WishlistServiceException NotFound(string message)
        => new(StatusCodes.Status404NotFound, message, null, false);

    public static WishlistServiceException BadRequest(string message, bool includeBody)
        => new(StatusCodes.Status400BadRequest, message, message, includeBody);

    public static WishlistServiceException ValidationFailed(Dictionary<string, string[]> errors, string message)
        => new(StatusCodes.Status400BadRequest, message, new ValidationErrorsResponse(errors), true);
}
