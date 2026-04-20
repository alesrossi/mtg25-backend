using API.Dtos.Cards;
using API.Dtos.Collections;
using API.Dtos.Teams;
using Core.Enums;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Services;

public interface ITeamCollectionService
{
    Task<IReadOnlyList<TeamCollectionViewDto>> GetCollectionViewsAsync(int teamId, string userId);
    Task<TeamCollectionViewDto> GetCollectionViewAsync(int viewId, string userId);
    Task<TeamCollectionViewDto> CreateCollectionViewAsync(int teamId, CreateTeamCollectionViewDto dto, string userId);
    Task<TeamCollectionViewDto> UpdateCollectionViewAsync(int viewId, UpdateTeamCollectionViewDto dto, string userId);
    Task DeleteCollectionViewAsync(int viewId, string userId);
    Task<IReadOnlyList<CardDto>> GetMergedCollectionCardsAsync(int viewId, string userId);
}

public sealed class TeamCollectionService : ITeamCollectionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<AppUser> _userManager;
    private readonly ITeamService _teamService;

    public TeamCollectionService(
        IUnitOfWork unitOfWork,
        UserManager<AppUser> userManager,
        ITeamService teamService)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _teamService = teamService;
    }

    public async Task<IReadOnlyList<TeamCollectionViewDto>> GetCollectionViewsAsync(int teamId, string userId)
    {
        if (!await _teamService.HasTeamAccessAsync(teamId, userId, TeamRole.Member))
            throw TeamCollectionServiceException.Problem(403, "Forbidden", "You do not have access to this team.");

        var views = await _unitOfWork.Repository<TeamCollectionView>().Query
            .AsNoTracking()
            .Where(v => v.TeamId == teamId)
            .ToListAsync();

        var result = new List<TeamCollectionViewDto>();

        foreach (var view in views)
        {
            var createdBy = await _userManager.FindByIdAsync(view.CreatedById);
            var collections = await GetCollectionSummariesAsync(view.CollectionIds);

            result.Add(new TeamCollectionViewDto
            {
                Id = view.Id,
                TeamId = view.TeamId,
                Name = view.Name,
                CollectionIds = view.CollectionIds,
                Collections = collections,
                CreatedAt = view.CreatedAt,
                CreatedByDisplayName = createdBy?.UserName ?? "Unknown",
                TotalCards = collections.Sum(c => c.CardsCount),
                TotalPrice = collections.Sum(c => c.TotalPrice)
            });
        }

        return result;
    }

    public async Task<TeamCollectionViewDto> GetCollectionViewAsync(int viewId, string userId)
    {
        var view = await _unitOfWork.Repository<TeamCollectionView>().Query
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == viewId);

        if (view is null)
            throw TeamCollectionServiceException.Problem(404, "Not Found", "Collection view not found.");

        if (!await _teamService.HasTeamAccessAsync(view.TeamId, userId, TeamRole.Member))
            throw TeamCollectionServiceException.Problem(403, "Forbidden", "You do not have access to this team.");

        var createdBy = await _userManager.FindByIdAsync(view.CreatedById);
        var collections = await GetCollectionSummariesAsync(view.CollectionIds);

        return new TeamCollectionViewDto
        {
            Id = view.Id,
            TeamId = view.TeamId,
            Name = view.Name,
            CollectionIds = view.CollectionIds,
            Collections = collections,
            CreatedAt = view.CreatedAt,
            CreatedByDisplayName = createdBy?.UserName ?? "Unknown",
            TotalCards = collections.Sum(c => c.CardsCount),
            TotalPrice = collections.Sum(c => c.TotalPrice)
        };
    }

    public async Task<TeamCollectionViewDto> CreateCollectionViewAsync(int teamId, CreateTeamCollectionViewDto dto, string userId)
    {
        if (!await _teamService.HasTeamAccessAsync(teamId, userId, TeamRole.Admin))
            throw TeamCollectionServiceException.Problem(403, "Forbidden", "Admin access required.");

        var view = new TeamCollectionView
        {
            TeamId = teamId,
            Name = dto.Name,
            CollectionIds = dto.CollectionIds,
            CreatedById = userId
        };

        _unitOfWork.Repository<TeamCollectionView>().Add(view);
        await _unitOfWork.Complete();

        return await GetCollectionViewAsync(view.Id, userId);
    }

    public async Task<TeamCollectionViewDto> UpdateCollectionViewAsync(int viewId, UpdateTeamCollectionViewDto dto, string userId)
    {
        var view = await _unitOfWork.Repository<TeamCollectionView>().Query
            .FirstOrDefaultAsync(v => v.Id == viewId);

        if (view is null)
            throw TeamCollectionServiceException.Problem(404, "Not Found", "Collection view not found.");

        if (!await _teamService.HasTeamAccessAsync(view.TeamId, userId, TeamRole.Admin))
            throw TeamCollectionServiceException.Problem(403, "Forbidden", "Admin access required.");

        if (dto.Name is not null)
            view.Name = dto.Name;

        if (dto.CollectionIds is not null)
            view.CollectionIds = dto.CollectionIds;

        await _unitOfWork.Complete();

        return await GetCollectionViewAsync(viewId, userId);
    }

    public async Task DeleteCollectionViewAsync(int viewId, string userId)
    {
        var view = await _unitOfWork.Repository<TeamCollectionView>().Query
            .FirstOrDefaultAsync(v => v.Id == viewId);

        if (view is null)
            throw TeamCollectionServiceException.Problem(404, "Not Found", "Collection view not found.");

        if (!await _teamService.HasTeamAccessAsync(view.TeamId, userId, TeamRole.Admin))
            throw TeamCollectionServiceException.Problem(403, "Forbidden", "Admin access required.");

        _unitOfWork.Repository<TeamCollectionView>().Remove(view);
        await _unitOfWork.Complete();
    }

    public async Task<IReadOnlyList<CardDto>> GetMergedCollectionCardsAsync(int viewId, string userId)
    {
        var view = await _unitOfWork.Repository<TeamCollectionView>().Query
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == viewId);

        if (view is null)
            throw TeamCollectionServiceException.Problem(404, "Not Found", "Collection view not found.");

        if (!await _teamService.HasTeamAccessAsync(view.TeamId, userId, TeamRole.Member))
            throw TeamCollectionServiceException.Problem(403, "Forbidden", "You do not have access to this team.");

        if (view.CollectionIds.Count == 0)
            return Array.Empty<CardDto>();

        var cards = await _unitOfWork.Repository<Card>().Query
            .AsNoTracking()
            .Where(c => view.CollectionIds.Contains(c.CollectionId))
            .ToListAsync();

        return cards.Select(MapToDto).ToList();
    }

    private async Task<List<CollectionSummaryDto>> GetCollectionSummariesAsync(List<int> collectionIds)
    {
        if (collectionIds.Count == 0)
            return new List<CollectionSummaryDto>();

        var collections = await _unitOfWork.Repository<Collection>().Query
            .AsNoTracking()
            .Where(c => collectionIds.Contains(c.Id))
            .ToListAsync();

        var result = new List<CollectionSummaryDto>();

        foreach (var collection in collections)
        {
            var owner = await _userManager.FindByIdAsync(collection.OwnerId);

            result.Add(new CollectionSummaryDto
            {
                Id = collection.Id,
                Name = collection.Name,
                CardsCount = collection.NumberOfCards,
                TotalPrice = collection.TotalPrice,
                OwnerDisplayName = owner?.UserName ?? "Unknown"
            });
        }

        return result;
    }

    private static CardDto MapToDto(Card card)
    {
        return new CardDto
        {
            Id = card.Id,
            Name = card.Name,
            ScryfallId = card.ScryfallId,
            OracleId = card.OracleId,
            CollectionId = card.CollectionId,
            Quantity = card.Quantity,
            Language = card.Language,
            Condition = card.Condition.ToString(),
            IsFoil = card.IsFoil,
            PurchasePrice = card.PurchasePrice,
            PurchasePriceCurrency = card.PurchasePriceCurrency,
            ImageUrl = card.ImageUrl,
            BackImageUrl = card.BackImageUrl,
            ArtCrop = card.ArtCrop,
            SetCode = card.SetCode,
            SetName = card.SetName,
            TypeLine = card.TypeLine,
            CollectorNumber = card.CollectorNumber,
            Rarity = card.Rarity,
            IsMisprint = card.IsMisprint,
            IsAltered = card.IsAltered
        };
    }
}

public sealed class TeamCollectionServiceException : Exception
{
    public TeamCollectionServiceException(string message) : base(message) { }

    public static TeamCollectionServiceException Problem(int statusCode, string title, string detail)
        => new TeamCollectionServiceException($"{title}: {detail}")
        {
            StatusCode = statusCode,
            Title = title,
            Detail = detail
        };

    public int StatusCode { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Detail { get; private set; } = string.Empty;
}
