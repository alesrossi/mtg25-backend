using API.Dtos.Wishlists;
using API.Extensions;
using Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Wishlists;

public static partial class WishlistsEndpoint
{
    public static void MapWishlistsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/wishlists")
            .WithTags("Wishlists")
            .WithProblemDetailsContract();

        MapWishlistQueries(group);
        MapWishlistCommands(group);
    }

    private static void MapWishlistQueries(RouteGroupBuilder group)
    {
        group.MapGet("/", GetWishlistsAsync)
            .RequireAuthorization()
            .WithSummary("Get wishlists for user")
            .Produces<IEnumerable<WishlistSummaryDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{id:int}", GetWishlistByIdAsync)
            .RequireAuthorization("OptionalAuth")
            .WithSummary("Get wishlist by ID (public wishlists accessible to all)")
            .Produces<WishlistDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{wishlistId:int}/cards", GetWishlistCardsAsync)
            .RequireAuthorization("OptionalAuth")
            .WithSummary("Get wishlist cards (public wishlists accessible to all)")
            .Produces<IEnumerable<WishlistCardDto>>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapGet("/{wishlistId:int}/cards/{cardId:int}", GetWishlistCardByIdAsync)
            .RequireAuthorization()
            .WithSummary("Get wishlist card")
            .Produces<WishlistCardDto>()
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    }

    private static void MapWishlistCommands(RouteGroupBuilder group)
    {
        group.MapPost("/", CreateWishlistAsync)
            .RequireAuthorization()
            .WithSummary("Create wishlist")
            .Produces<WishlistDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPut("/{id:int}", UpdateWishlistAsync)
            .RequireAuthorization()
            .WithSummary("Update wishlist")
            .Produces<WishlistDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapDelete("/{id:int}", DeleteWishlistAsync)
            .RequireAuthorization()
            .WithSummary("Delete wishlist")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPost("/{wishlistId:int}/cards", CreateWishlistCardAsync)
            .RequireAuthorization()
            .WithSummary("Add card list to wishlist")
            .Produces<List<WishlistCard>>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPut("/{wishlistId:int}/cards/{cardId:int}", UpdateWishlistCardAsync)
            .RequireAuthorization()
            .WithSummary("Update wishlist card")
            .Produces<WishlistCardDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapDelete("/{wishlistId:int}/cards/{cardId:int}", DeleteWishlistCardAsync)
            .RequireAuthorization()
            .WithSummary("Delete wishlist card")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    }
}
