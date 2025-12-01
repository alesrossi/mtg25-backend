using API.Extensions;

namespace API.Endpoints.Accounts;

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
