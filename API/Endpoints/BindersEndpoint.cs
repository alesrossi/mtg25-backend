using System.Collections.Generic;
using API.Dtos.Binders;
using Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API.Endpoints;

public static partial class BindersEndpoint
{
    public static void MapBindersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/binders").WithTags("Binders");

        MapBinderQueries(group);
        MapBinderCommands(group);
    }
}
