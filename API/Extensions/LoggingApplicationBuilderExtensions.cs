using API.Middleware;

namespace API.Extensions;

public static class LoggingApplicationBuilderExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestLoggingMiddleware>();
    }
}
