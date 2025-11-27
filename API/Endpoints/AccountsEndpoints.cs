using API.Dtos.Accounts;
using API.Extensions;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API.Endpoints;

public static partial class AccountsEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/accounts")
            .WithTags("Accounts")
            .WithProblemDetailsContract();

        MapAccountQueries(group);
        MapAccountCommands(group);
    }
}
