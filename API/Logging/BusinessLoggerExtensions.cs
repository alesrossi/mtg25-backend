using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace API.Logging;

public static class BusinessLoggerExtensions
{
    public static IDisposable BeginOperationScope<T>(this ILogger<T> logger, string operationName, object? entityId = null)
    {
        var scopeData = entityId is null
            ? new Dictionary<string, object?> { ["operation"] = operationName }
            : new Dictionary<string, object?> { ["operation"] = operationName, ["entityId"] = entityId };

        return logger.BeginScope(scopeData) ?? NullScope.Instance;
    }

    public static void LogOperationStart<T>(this ILogger<T> logger, string operationName, object? details = null)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("{Operation} started {@Details}", operationName, details);
        }
    }

    public static void LogOperationStep<T>(this ILogger<T> logger, string operationName, string message, object? details = null)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("{Operation}: {Message} {@Details}", operationName, message, details);
        }
    }

    public static void LogOperationSuccess<T>(this ILogger<T> logger, string operationName, object? result = null)
    {
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("{Operation} completed {@Result}", operationName, result);
        }
    }

    public static void LogOperationWarning<T>(this ILogger<T> logger, string operationName, string reason, object? details = null)
    {
        logger.LogWarning("{Operation} warning: {Reason} {@Details}", operationName, reason, details);
    }

    public static void LogOperationFailure<T>(this ILogger<T> logger, string operationName, Exception exception, object? details = null)
    {
        logger.LogError(exception, "{Operation} failed {@Details}", operationName, details);
    }
}

internal sealed class NullScope : IDisposable
{
    public static readonly NullScope Instance = new();

    private NullScope()
    {
    }

    public void Dispose()
    {
    }
}
