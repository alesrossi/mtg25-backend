using API.Dtos;
using API.Helpers;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
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
        
        app.MapGet("/collections/{id}/cards", async (IUnitOfWork unitOfWork, int id, [AsParameters]CardsSpecParams cardsParams) =>
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
        });

        
        app.MapPost("/collections", async (IUnitOfWork unitOfWork, NewCollectionDto collectionDto) =>
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
        });
    }
}