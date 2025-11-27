using API.Dtos.Leagues;
using API.Extensions;
using API.Services;
using Core.Models.Identity;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Endpoints;

public static partial class LeaguesEndpoint
{
    public static void MapLeaguesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/leagues")
            .WithTags("Leagues")
            .WithProblemDetailsContract();

        MapLeagueQueries(group);
        MapLeagueCommands(group);
    }
}
