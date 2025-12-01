using API.Dtos.Wishlists;
using API.Extensions;
using Core.Models;

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
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:int}", GetWishlistByIdAsync)
            .RequireAuthorization()
            .WithSummary("Get wishlist by ID")
            .Produces<WishlistDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{wishlistId:int}/cards", GetWishlistCardsAsync)
            .RequireAuthorization()
            .WithSummary("Get wishlist cards")
            .Produces<IEnumerable<WishlistCardDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{wishlistId:int}/cards/{cardId:int}", GetWishlistCardByIdAsync)
            .RequireAuthorization()
            .WithSummary("Get wishlist card")
            .Produces<WishlistCardDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static void MapWishlistCommands(RouteGroupBuilder group)
    {
        group.MapPost("/", CreateWishlistAsync)
            .RequireAuthorization()
            .WithSummary("Create wishlist")
            .Produces<WishlistDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPut("/{id:int}", UpdateWishlistAsync)
            .RequireAuthorization()
            .WithSummary("Update wishlist")
            .Produces<WishlistDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:int}", DeleteWishlistAsync)
            .RequireAuthorization()
            .WithSummary("Delete wishlist")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{wishlistId:int}/cards", CreateWishlistCardAsync)
            .RequireAuthorization()
            .WithSummary("Add card list to wishlist")
            .Produces<List<WishlistCard>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{wishlistId:int}/cards/{cardId:int}", UpdateWishlistCardAsync)
            .RequireAuthorization()
            .WithSummary("Update wishlist card")
            .Produces<WishlistCardDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{wishlistId:int}/cards/{cardId:int}", DeleteWishlistCardAsync)
            .RequireAuthorization()
            .WithSummary("Delete wishlist card")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }
}
