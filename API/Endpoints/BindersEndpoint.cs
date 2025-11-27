using System.Collections.Generic;
using API.Dtos.Binders;
using API.Extensions;
using Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API.Endpoints;

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
}
