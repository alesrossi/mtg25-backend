using API.Extensions;

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
}
