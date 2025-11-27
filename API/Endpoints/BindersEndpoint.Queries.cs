using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using API.Dtos.Binders;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;
using Microsoft.AspNetCore.Routing;

namespace API.Endpoints;

public static partial class BindersEndpoint
{
    private static void MapBinderQueries(RouteGroupBuilder group)
    {
        group.MapGet("/", GetBindersAsync)
            .RequireAuthorization()
            .WithSummary("Get binders for user")
            .Produces<IEnumerable<BinderSummaryDto>>()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:int}", GetBinderByIdAsync)
            .RequireAuthorization()
            .WithSummary("Get binder by ID")
            .Produces<BinderDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{binderId:int}/cards", GetBinderCardsAsync)
            .RequireAuthorization()
            .WithSummary("Get binder cards")
            .Produces<IEnumerable<BinderCardDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{binderId:int}/cards/{binderCardId:int}", GetBinderCardByIdAsync)
            .RequireAuthorization()
            .WithSummary("Get binder card")
            .Produces<BinderCardDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetBindersAsync(
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var spec = new TradeBindersWithOwnerSpecification(userId, includeCards: true);
        var binders = await unitOfWork.Repository<TradeBinder>().ListAsync(spec, tracking: false) ?? Array.Empty<TradeBinder>();

        var dto = binders.Select(MapToSummaryDto).ToList();
        return Results.Ok(dto);
    }

    private static async Task<IResult> GetBinderByIdAsync(
        int id,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var spec = new TradeBinderWithCardsSpecification(id);
        var binder = await unitOfWork.Repository<TradeBinder>().GetEntityWithSpec(spec, tracking: false);
        if (binder == null) return Results.NotFound();

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isOwner = !string.IsNullOrEmpty(userId) && binder.OwnerId == userId;

        if (!isOwner && !binder.IsPublic)
        {
            return Results.Unauthorized();
        }

        var cards = await unitOfWork.Repository<BinderCard>()
            .ListAsync(new BinderCardsWithBinderIdSpecification(binder.Id), tracking: false) ?? Array.Empty<BinderCard>();

        return Results.Ok(MapToDto(binder, cards));
    }

    private static async Task<IResult> GetBinderCardsAsync(
        int binderId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var (result, binder) = await EnsureBinderAccessAsync(binderId, unitOfWork, user, allowPublic: true, tracking: false);
        if (result != null) return result;

        var cards = await unitOfWork.Repository<BinderCard>()
            .ListAsync(new BinderCardsWithBinderIdSpecification(binder!.Id), tracking: false) ?? Array.Empty<BinderCard>();

        var dto = cards.Select(MapToDto).ToList();
        return Results.Ok(dto);
    }

    private static async Task<IResult> GetBinderCardByIdAsync(
        int binderId,
        int binderCardId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user)
    {
        var (result, binder) = await EnsureBinderAccessAsync(binderId, unitOfWork, user, allowPublic: true, tracking: false);
        if (result != null) return result;

        var binderCard = await unitOfWork.Repository<BinderCard>().GetEntityWithSpec(new BinderCardWithBinderSpecification(binderCardId), tracking: false);
        if (binderCard == null || binderCard.TradeBinderId != binder!.Id)
        {
            return Results.NotFound();
        }

        return Results.Ok(MapToDto(binderCard));
    }
}
