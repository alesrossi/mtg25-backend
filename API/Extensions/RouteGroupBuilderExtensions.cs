using API.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace API.Extensions;

public static class RouteGroupBuilderExtensions
{
    public static RouteGroupBuilder WithProblemDetailsContract(this RouteGroupBuilder group)
    {
        group.AddEndpointFilter<ProblemDetailsEndpointFilter>();
        return group;
    }
}
