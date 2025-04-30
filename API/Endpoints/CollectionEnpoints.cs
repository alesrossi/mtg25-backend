using Core.Interfaces;
using Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace API.Endpoints;

public static class CollectionEnpoints
{
    public static void MapCollectionsEndpoints(this WebApplication app)
    {
        app.MapGet("/collections/{id}", async (IUnitOfWork unitOfWork, int id) =>
        {
            var collection = await unitOfWork.Repository<Collection>().GetByIdAsync(id);
            if (collection is not null)
            {
                return Results.Ok(collection);
            }
            return Results.NotFound("Collection not found");
        });

        
        app.MapPost("/collections", async (IUnitOfWork unitOfWork, Collection collection) =>
        {
            unitOfWork.Repository<Collection>().Add(collection);
            await unitOfWork.Complete();
            return Results.Ok(collection);
        });
    }
}