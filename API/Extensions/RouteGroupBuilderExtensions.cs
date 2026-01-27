using API.Filters;

namespace API.Extensions;

public static class RouteGroupBuilderExtensions
{
    public static RouteGroupBuilder WithProblemDetailsContract(this RouteGroupBuilder group)
    {
        group.AddEndpointFilter<ProblemDetailsEndpointFilter>();
        return group;
    }
}
