using API.Dtos;
using API.Helpers;
using API.Scryfall;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;

namespace API.Endpoints;

public static class CollectionEnpoints
{
    public static void MapCollectionsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/collections").WithTags("Collections");
        group.MapGet("/{id}", GetCollectionFromIdAsync)
            .WithSummary("Get collection by ID")
            .WithDescription("Retrieves a specific collection from the database using its unique identifier");
        group.MapGet("/{id}/cards", GetCardsFromCollectionAsync)
            .WithSummary("Get cards from collection")
            .WithDescription("Retrieves paginated list of cards from a specific collection with optional filtering and sorting parameters");
        group.MapPost("/", AddNewCollectionAsync)
            .WithSummary("Create new collection")
            .WithDescription("Creates a new collection with specified name and color properties, initializing card count and total price to zero");
        group.MapPost("/{id}/import", ImportCardList)
            .WithSummary("Import cards from CSV file")
            .WithDescription("Imports cards from a CSV file into a specific collection, processing the file and updating collection statistics")
            .DisableAntiforgery();
    }
    
    private static async Task<IResult> GetCollectionFromIdAsync (IUnitOfWork unitOfWork, int id)
    {
        var collection = await unitOfWork.Repository<Collection>().GetByIdAsync(id);
        if (collection is not null)
        {
            return Results.Ok(collection);
        }
        return Results.NotFound("Collection not found");
    }
    
    private static async Task<IResult>  GetCardsFromCollectionAsync (IUnitOfWork unitOfWork, int id, [AsParameters]CardsSpecParams cardsParams)
    {
        var collection = await unitOfWork.Repository<Collection>().GetByIdAsync(id);
        if (collection is not null)
        {
            var spec = new CardsWithParamsSpecification(cardsParams, id);
            var size = await unitOfWork.Repository<Card>().CountAsync(spec);
            var cards = await unitOfWork.Repository<Card>().ListAsync(spec);
            return Results.Ok(new Pagination<Card>(cardsParams.PageIndex, cardsParams.PageSize, size, cards));
        }
        return Results.NotFound("Collection not found");
    }
    
    private static async Task<IResult> AddNewCollectionAsync (IUnitOfWork unitOfWork, NewCollectionDto collectionDto)
    {
        var collection = new Collection
        {
            Name = collectionDto.Name,
            Color = collectionDto.Color,
            NumberOfCards = 0,
            TotalPrice = 0
        };
        unitOfWork.Repository<Collection>().Add(collection);
        await unitOfWork.Complete();
        return Results.Ok(collection);
    }
    
    private static async Task<IResult> ImportCardList (IUnitOfWork unitOfWork, CardDataService cds, IFormFile file, int id)
    {
        try
        {
            if (file.Length <= 0) return Results.BadRequest("No file uploaded");

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return Results.BadRequest("File must be csv");
                
            if (file.Length > 10 * 1024 * 1024) return Results.BadRequest("File is too large");

            var records = await CollectionHelpers.ProcessCsvFIle(file, cds, id);

            var col = await unitOfWork.Repository<Collection>().GetByIdAsync(id);
            col.NumberOfCards += records.Count;
                
            unitOfWork.Repository<Card>().Add(records);
            unitOfWork.Repository<Collection>().Update(col);
            await unitOfWork.Complete();
                
            return Results.Ok(records);
        }
        catch (Exception e)
        {
            return Results.StatusCode(500);
        }
    }
}