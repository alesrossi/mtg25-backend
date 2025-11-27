using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Context;
using API.Configuration;

namespace API.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<RequestLoggingMiddleware> logger;
    private readonly RequestLoggingOptions options;

    private const string CorrelationIdItemKey = "CorrelationId";

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        IOptions<RequestLoggingOptions> options)
    {
        this.next = next;
        this.logger = logger;
        this.options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headerName = string.IsNullOrWhiteSpace(options.CorrelationHeaderName)
            ? RequestLoggingOptions.DefaultCorrelationHeaderName
            : options.CorrelationHeaderName;

        var correlationId = ResolveCorrelationId(context, headerName);

        context.Items[CorrelationIdItemKey] = correlationId;
        context.Response.Headers[headerName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationIdItemKey] = correlationId
        }))
        {
            var stopwatch = Stopwatch.StartNew();
            var request = context.Request;
            var requestPath = request.Path + request.QueryString;
            logger.LogInformation(
                "Handling HTTP {Method} {Path} from {RemoteIp}",
                request.Method,
                requestPath,
                context.Connection.RemoteIpAddress);

            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unhandled exception while processing HTTP {Method} {Path}",
                    request.Method,
                    requestPath);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                var elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                var statusCode = context.Response?.StatusCode ?? StatusCodes.Status200OK;
                var level = MapToLogLevel(statusCode);
                var slowThreshold = Math.Max(options.SlowRequestThresholdMs, 0);
                var isSlow = slowThreshold > 0 && elapsedMilliseconds > slowThreshold;

                if (isSlow && level < LogLevel.Warning)
                {
                    level = LogLevel.Warning;
                }

                var message = isSlow
                    ? "HTTP {Method} {Path} responded {StatusCode} in {Elapsed:0.00} ms (slow)"
                    : "HTTP {Method} {Path} responded {StatusCode} in {Elapsed:0.00} ms";

                logger.Log(
                    level,
                    message,
                    request.Method,
                    requestPath,
                    statusCode,
                    elapsedMilliseconds);
            }
        }
    }

    private static LogLevel MapToLogLevel(int statusCode)
    {
        return statusCode switch
        {
            >= 500 => LogLevel.Error,
            >= 400 => LogLevel.Warning,
            _ => LogLevel.Information
        };
    }

    private static string ResolveCorrelationId(HttpContext context, string headerName)
    {
        if (context.Request.Headers.TryGetValue(headerName, out var provided) &&
            !string.IsNullOrWhiteSpace(provided))
        {
            return provided.ToString();
        }

        if (Activity.Current?.TraceId != null)
        {
            return Activity.Current.TraceId.ToString();
        }

        return context.TraceIdentifier;
    }
}
