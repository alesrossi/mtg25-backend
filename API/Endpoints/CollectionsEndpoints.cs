using System.Security.Claims;
using API.Dtos.Cards;
using API.Dtos.Collections;
using API.Helpers;
using API.Services;
using Core.Interfaces;
using Core.Models;
using Core.Models.Identity;
using Core.Specifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints;

public static class CollectionsEndpoints
{
    public static void MapCollectionsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/collections").WithTags("Collections");
        group.MapGet("/{id:int}", GetCollectionFromIdAsync)
            .RequireAuthorization()
            .WithSummary("Get collection by ID")
            .WithDescription("Retrieves specific collection by ID")
            .Produces<Collection>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapGet("/{id:int}/cards", GetCardsFromCollectionAsync)
            .RequireAuthorization()
            .WithSummary("Get cards from collection")
            .WithDescription("Retrieves paginated list of cards from a specific collection with filtering, sorting, searching, and optional grouping")
            .Produces<Pagination<Card>>()
            .Produces<GroupedCardsPaginationDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapPost("/", AddNewCollectionAsync)
            .RequireAuthorization()
            .WithSummary("Create new collection")
            .WithDescription("Creates new collection with name and color")
            .Produces<Collection>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
        
        group.MapPatch("/{id:int}/import", ImportCardList)
            .RequireAuthorization()
            .WithSummary("Import cards from CSV")
            .WithDescription("Imports cards from CSV file into collection")
            .Produces<List<Card>>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .DisableAntiforgery();
        
        group.MapGet("/", GetAllCollectionsForUser)
            .RequireAuthorization()
            .WithSummary("Get user's collections")
            .WithDescription("Returns all collections owned by authenticated user")
            .Produces<List<Collection>>()
            .Produces(StatusCodes.Status401Unauthorized);
        
        group.MapDelete("/{id:int}/mass-delete", MassDeleteCardsFromCollection)
            .RequireAuthorization()
            .WithSummary("Mass delete cards")
            .WithDescription("Deletes multiple cards from collection by ID list")
            .Produces<int>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }
    
    private static async Task<IResult> GetCollectionFromIdAsync(
        IUnitOfWork unitOfWork, 
        int id,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        var collection = await unitOfWork.Repository<Collection>().GetByIdAsync(id);
        if (collection is null) return Results.NotFound();
        
        return collection.OwnerId != userId ? Results.Unauthorized() : Results.Ok(collection);
    }
    
    private static async Task<IResult>  GetCardsFromCollectionAsync(
        IUnitOfWork unitOfWork, 
        int id, 
        [AsParameters]EntitySpecParams entityParams,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        var collection = await unitOfWork.Repository<Collection>().GetByIdAsync(id);
        if (collection is null) return Results.NotFound();
        if (collection.OwnerId != userId) return Results.Unauthorized();

        // Handle grouping if requested
        if (!string.IsNullOrEmpty(entityParams.GroupBy))
        {
            return await GetGroupedCardsFromCollectionAsync(unitOfWork, id, entityParams);
        }
        
        var spec = new CardsWithParamsSpecification(entityParams, id);
        var size = await unitOfWork.Repository<Card>().CountAsync(spec);
        var cards = await unitOfWork.Repository<Card>().ListAsync(spec);
        
        return Results.Ok(new Pagination<Card>(entityParams.PageIndex, entityParams.PageSize, size, cards));
    }
    
    private static async Task<IResult> AddNewCollectionAsync (
        [FromServices]IUnitOfWork unitOfWork, 
        [FromServices] UserManager<AppUser> userManager, 
        [FromServices] IValidationService validationService,
        HttpContext context, 
        NewCollectionDto collectionDto)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        
        // Validate the model
        var (isValid, errors) = validationService.ValidateModel(collectionDto);
        if (!isValid)
        {
            return Results.BadRequest(new { errors });
        }
        
        var collection = new Collection
        {
            Name = collectionDto.Name,
            Color = collectionDto.Color,
            NumberOfCards = 0,
            TotalPrice = 0,
            OwnerId = user!.Id
        };
        unitOfWork.Repository<Collection>().Add(collection);
        await unitOfWork.Complete();
        return Results.Ok(collection);
    }
    
    private static async Task<IResult> ImportCardList(
        IUnitOfWork unitOfWork, 
        CardDataService cds, 
        IFormFile file,
        int id,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        try
        {
            if (file.Length <= 0) return Results.BadRequest("No file uploaded");

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return Results.BadRequest("File must be csv");
                
            if (file.Length > 10 * 1024 * 1024) return Results.BadRequest("File is too large");

            var records = await CollectionHelpers.ProcessCsvFIle(file, cds, id);

            var collection = await unitOfWork.Repository<Collection>().GetByIdAsync(id);
            if (collection is null) return Results.NotFound();
            if (collection.OwnerId != userId) return Results.Unauthorized();
            
            collection.NumberOfCards += records.Count;
                
            unitOfWork.Repository<Card>().Add(records);
            unitOfWork.Repository<Collection>().Update(collection);
            await unitOfWork.Complete();
                
            return Results.Ok(records);
        }
        catch (Exception)
        {
            return Results.StatusCode(500);
        }
    }
    
    private static async Task<IResult> GetAllCollectionsForUser(
        IUnitOfWork unitOfWork, 
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();
        
        var collections = await unitOfWork.Repository<Collection>().ListAsync(new CollectionWithOwnerSpecification(userId));
        return Results.Ok(collections);
    }
    
    private static async Task<IResult> MassDeleteCardsFromCollection(
        IUnitOfWork unitOfWork, 
        int id,
        [FromBody] List<int> ctbd,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        var deletedCount = await unitOfWork.Repository<Card>().Delete(ctbd);
        await unitOfWork.Complete();
        
        return Results.Ok(deletedCount);
    }

    private static async Task<IResult> GetGroupedCardsFromCollectionAsync(
        IUnitOfWork unitOfWork,
        int collectionId,
        EntitySpecParams entityParams)
    {
        var spec = new CardsWithParamsSpecification(entityParams, collectionId);
        var allCards = await unitOfWork.Repository<Card>().ListAsync(spec);
        
        if (allCards == null || !allCards.Any())
        {
            return Results.Ok(new GroupedCardsPaginationDto
            {
                PageIndex = entityParams.PageIndex,
                PageSize = entityParams.PageSize,
                TotalGroups = 0,
                TotalCards = 0,
                Groups = []
            });
        }
        
        var groupedCards = entityParams.GroupBy?.ToLower() switch
        {
            "setname" => allCards.GroupBy(c => c.SetName).Select(g => new GroupedCardsDto
            {
                GroupKey = g.Key,
                Count = g.Count(),
                Cards = g.ToList()
            }).ToList(),
            "setcode" => allCards.GroupBy(c => c.SetCode).Select(g => new GroupedCardsDto
            {
                GroupKey = g.Key,
                Count = g.Count(),
                Cards = g.ToList()
            }).ToList(),
            "rarity" => allCards.GroupBy(c => c.Rarity).Select(g => new GroupedCardsDto
            {
                GroupKey = g.Key,
                Count = g.Count(),
                Cards = g.ToList()
            }).ToList(),
            "condition" => allCards.GroupBy(c => c.Condition.ToString()).Select(g => new GroupedCardsDto
            {
                GroupKey = g.Key,
                Count = g.Count(),
                Cards = g.ToList()
            }).ToList(),
            "language" => allCards.GroupBy(c => c.Language).Select(g => new GroupedCardsDto
            {
                GroupKey = g.Key,
                Count = g.Count(),
                Cards = g.ToList()
            }).ToList(),
            _ => allCards.GroupBy(c => c.Name).Select(g => new GroupedCardsDto
            {
                GroupKey = g.Key,
                Count = g.Count(),
                Cards = g.ToList()
            }).ToList()
        };

        var totalGroups = groupedCards.Count;
        var totalCards = allCards.Count;
        
        // Apply pagination to groups
        var paginatedGroups = groupedCards
            .Skip(entityParams.PageSize * (entityParams.PageIndex - 1))
            .Take(entityParams.PageSize)
            .ToList();

        var result = new GroupedCardsPaginationDto
        {
            PageIndex = entityParams.PageIndex,
            PageSize = entityParams.PageSize,
            TotalGroups = totalGroups,
            TotalCards = totalCards,
            Groups = paginatedGroups
        };

        return Results.Ok(result);
    }
}