using API.Extensions;

namespace API.Endpoints.Collections;

public static partial class CollectionsEndpoints
{
    public static void MapCollectionsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/collections")
            .WithTags("Collections")
            .WithProblemDetailsContract();

        MapCollectionQueries(group);
        MapCollectionCommands(group);
    }
}
