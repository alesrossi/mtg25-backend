using System.Security.Claims;
using API.Dtos.Binders;
using API.Logging;
using Core.Interfaces;
using Core.Models;
using Core.Specifications;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Binders;

public static partial class BindersEndpoint
{
    private static void MapBinderQueries(RouteGroupBuilder group)
    {
        group.MapGet("/", GetBindersAsync)
            .RequireAuthorization()
            .WithSummary("Get binders for user")
            .Produces<IEnumerable<BinderSummaryDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{id:int}", GetBinderByIdAsync)
            .RequireAuthorization()
            .WithSummary("Get binder by ID")
            .Produces<BinderDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{binderId:int}/cards", GetBinderCardsAsync)
            .RequireAuthorization()
            .WithSummary("Get binder cards")
            .Produces<IEnumerable<BinderCardDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{binderId:int}/cards/{binderCardId:int}", GetBinderCardByIdAsync)
            .RequireAuthorization()
            .WithSummary("Get binder card")
            .Produces<BinderCardDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    }

    private static async Task<IResult> GetBindersAsync(
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.List";
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogOperationWarning(operation, "Missing user id");
            return Results.Unauthorized();
        }

        var spec = new TradeBindersWithOwnerSpecification(userId, includeCards: true);
        var binders = await unitOfWork.Repository<TradeBinder>().ListAsync(spec, tracking: false) ?? Array.Empty<TradeBinder>();

        var dto = binders.Select(MapToSummaryDto).ToList();
        logger.LogOperationSuccess(operation, new { userId, Count = dto.Count });
        return Results.Ok(dto);
    }

    private static async Task<IResult> GetBinderByIdAsync(
        int id,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Get";
        var spec = new TradeBinderWithCardsSpecification(id);
        var binder = await unitOfWork.Repository<TradeBinder>().GetEntityWithSpec(spec, tracking: false);
        if (binder == null)
        {
            logger.LogOperationWarning(operation, "Binder not found", new { id });
            return Results.NotFound();
        }

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isOwner = !string.IsNullOrEmpty(userId) && binder.OwnerId == userId;

        if (!isOwner && !binder.IsPublic)
        {
            logger.LogOperationWarning(operation, "Unauthorized", new { id, userId });
            return Results.Unauthorized();
        }

        var cards = await unitOfWork.Repository<BinderCard>()
            .ListAsync(new BinderCardsWithBinderIdSpecification(binder.Id), tracking: false) ?? Array.Empty<BinderCard>();

        logger.LogOperationSuccess(operation, new { id, Cards = cards.Count });
        return Results.Ok(MapToDto(binder, cards));
    }

    private static async Task<IResult> GetBinderCardsAsync(
        int binderId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Cards.List";
        var (result, binder) = await EnsureBinderAccessAsync(binderId, unitOfWork, user, allowPublic: true, tracking: false);
        if (result != null)
        {
            logger.LogOperationWarning(operation, "Access denied", new { binderId });
            return result;
        }

        var cards = await unitOfWork.Repository<BinderCard>()
            .ListAsync(new BinderCardsWithBinderIdSpecification(binder!.Id), tracking: false) ?? Array.Empty<BinderCard>();

        var dto = cards.Select(MapToDto).ToList();
        logger.LogOperationSuccess(operation, new { binderId, Count = dto.Count });
        return Results.Ok(dto);
    }

    private static async Task<IResult> GetBinderCardByIdAsync(
        int binderId,
        int binderCardId,
        IUnitOfWork unitOfWork,
        ClaimsPrincipal user,
        [FromServices] ILogger<BindersEndpointLogCategory> logger)
    {
        const string operation = "Binders.Cards.Get";
        var (result, binder) = await EnsureBinderAccessAsync(binderId, unitOfWork, user, allowPublic: true, tracking: false);
        if (result != null)
        {
            logger.LogOperationWarning(operation, "Access denied", new { binderId, binderCardId });
            return result;
        }

        var binderCard = await unitOfWork.Repository<BinderCard>().GetEntityWithSpec(new BinderCardWithBinderSpecification(binderCardId), tracking: false);
        if (binderCard == null || binderCard.TradeBinderId != binder!.Id)
        {
            logger.LogOperationWarning(operation, "Binder card not found", new { binderId, binderCardId });
            return Results.NotFound();
        }

        logger.LogOperationSuccess(operation, new { binderId, binderCardId });
        return Results.Ok(MapToDto(binderCard));
    }
}
