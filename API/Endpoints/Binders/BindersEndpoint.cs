using API.Dtos.Binders;
using API.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace API.Endpoints.Binders;

public static partial class BindersEndpoint
{
    public static void MapBindersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/binders")
            .WithTags("Binders")
            .WithProblemDetailsContract();

        MapBinderQueries(group);
        MapBinderCommands(group);
    }
    
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
    
    private static void MapBinderCommands(RouteGroupBuilder group)
    {
        group.MapPost("/", CreateBinderAsync)
            .RequireAuthorization()
            .WithSummary("Create binder")
            .Produces<BinderDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPut("/{id:int}", UpdateBinderAsync)
            .RequireAuthorization()
            .WithSummary("Update binder")
            .Produces<BinderDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapDelete("/{id:int}", DeleteBinderAsync)
            .RequireAuthorization()
            .WithSummary("Delete binder")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPost("/{binderId:int}/cards", CreateBinderCardAsync)
            .RequireAuthorization()
            .WithSummary("Add card to binder")
            .Produces<List<BinderCardDto>>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapPut("/{binderId:int}/cards/{binderCardId:int}", UpdateBinderCardAsync)
            .RequireAuthorization()
            .WithSummary("Update binder card")
            .Produces<BinderCardDto>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

        group.MapDelete("/{binderId:int}/cards/{binderCardId:int}", DeleteBinderCardAsync)
            .RequireAuthorization()
            .WithSummary("Delete binder card")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, contentType: "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, contentType: "application/problem+json");
    }
    
}
