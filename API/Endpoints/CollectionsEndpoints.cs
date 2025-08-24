using System.Security.Claims;
using API.Dtos;
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
            .WithDescription("Retrieves a specific collection from the database using its unique identifier")
            .Produces<Collection>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapGet("/{id:int}/cards", GetCardsFromCollectionAsync)
            .RequireAuthorization()
            .WithSummary("Get cards from collection")
            .WithDescription("Retrieves paginated list of cards from a specific collection with optional filtering and sorting parameters")
            .Produces<Pagination<Card>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
        
        group.MapPost("/", AddNewCollectionAsync)
            .RequireAuthorization()
            .WithSummary("Create new collection")
            .WithDescription("Creates a new collection with specified name and color properties, initializing card count and total price to zero")
            .Produces<Collection>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
        
        group.MapPatch("/{id:int}/import", ImportCardList)
            .RequireAuthorization()
            .WithSummary("Import cards from CSV file")
            .WithDescription(
                "Imports cards from a CSV file into a specific collection, processing the file and updating collection statistics")
            .Produces<List<Card>>() // Returns deleted count
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .DisableAntiforgery();
        
        group.MapGet("/", GetAllCollectionsForUser)
            .RequireAuthorization()
            .WithSummary("Lists all collections for authenticated user")
            .WithDescription("Lists all collections for authenticated user")
            .Produces<List<Collection>>()
            .Produces(StatusCodes.Status401Unauthorized);
        
        group.MapDelete("/{id:int}/mass-delete", MassDeleteCardsFromCollection)
            .RequireAuthorization()
            .WithSummary("Deletes multiple cards from a specific collection")
            .WithDescription("Deletes multiple cards from a specific collection")
            .Produces<int>() // Returns deleted count
            .Produces(StatusCodes.Status401Unauthorized);
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
}