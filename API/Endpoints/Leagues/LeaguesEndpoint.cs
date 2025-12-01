using API.Extensions;

namespace API.Endpoints.Leagues;

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
