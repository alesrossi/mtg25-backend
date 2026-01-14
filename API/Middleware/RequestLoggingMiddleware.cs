using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
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
            var captureRequestBody = options.IncludeRequestBody &&
                (request.ContentLength ?? 0) > 0 &&
                request.Body.CanRead &&
                IsTextContentType(request.ContentType);
            var captureResponseBody = options.IncludeResponseBody;
            string? requestBody = null;
            string? responseBody = null;
            Stream? originalResponseBody = null;
            MemoryStream? bufferedResponseBody = null;

            if (captureRequestBody)
            {
                requestBody = await ReadRequestBodyAsync(request);
            }

            if (captureResponseBody)
            {
                originalResponseBody = context.Response.Body;
                bufferedResponseBody = new MemoryStream();
                context.Response.Body = bufferedResponseBody;
            }

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
                if (captureResponseBody && bufferedResponseBody != null)
                {
                    if (IsTextContentType(context.Response.ContentType))
                    {
                        responseBody = await ReadResponseBodyAsync(context, bufferedResponseBody);
                    }

                    bufferedResponseBody.Seek(0, SeekOrigin.Begin);
                    if (originalResponseBody != null)
                    {
                        await bufferedResponseBody.CopyToAsync(originalResponseBody);
                        context.Response.Body = originalResponseBody;
                    }
                }

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

                if (captureRequestBody && !string.IsNullOrWhiteSpace(requestBody))
                {
                    logger.LogInformation(
                        "HTTP {Method} {Path} request body: {RequestBody}",
                        request.Method,
                        requestPath,
                        requestBody);
                }

                if (captureResponseBody && !string.IsNullOrWhiteSpace(responseBody))
                {
                    logger.LogInformation(
                        "HTTP {Method} {Path} response body: {ResponseBody}",
                        request.Method,
                        requestPath,
                        responseBody);
                }
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

    private async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        request.Body.Seek(0, SeekOrigin.Begin);
        var body = await ReadStreamAsync(request.Body);
        request.Body.Seek(0, SeekOrigin.Begin);
        return body;
    }

    private async Task<string> ReadResponseBodyAsync(HttpContext context, Stream responseBody)
    {
        responseBody.Seek(0, SeekOrigin.Begin);

        if (!IsTextContentType(context.Response.ContentType))
        {
            return string.Empty;
        }

        var contentEncoding = context.Response.Headers[HeaderNames.ContentEncoding].ToString();
        if (string.IsNullOrWhiteSpace(contentEncoding))
        {
            return await ReadStreamAsync(responseBody);
        }

        await using var decodedStream = await TryDecodeToMemoryStreamAsync(responseBody, contentEncoding);
        if (decodedStream == null)
        {
            return string.Empty;
        }

        decodedStream.Seek(0, SeekOrigin.Begin);
        return await ReadStreamAsync(decodedStream);
    }

    private async Task<string> ReadStreamAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var text = await reader.ReadToEndAsync();
        return TruncateBody(text);
    }

    private string TruncateBody(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return body;
        }

        var limit = Math.Max(options.BodySizeLimitKb, 1) * 1024;
        if (body.Length <= limit)
        {
            return body;
        }

        return body[..limit] + "... (truncated)";
    }

    private static bool IsTextContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var mediaType = contentType;
        var separatorIndex = contentType.IndexOf(';');
        if (separatorIndex >= 0)
        {
            mediaType = contentType[..separatorIndex];
        }

        mediaType = mediaType.Trim();
        if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
            mediaType.Equals("application/problem+json", StringComparison.OrdinalIgnoreCase) ||
            mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Stream?> TryDecodeToMemoryStreamAsync(Stream source, string contentEncoding)
    {
        var encodings = contentEncoding.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (encodings.Length == 0)
        {
            return null;
        }

        Stream current = source;
        var wrappers = new Stack<Stream>();

        try
        {
            for (var i = encodings.Length - 1; i >= 0; i--)
            {
                var encoding = encodings[i].ToLowerInvariant();
                if (encoding == "identity")
                {
                    continue;
                }

                Stream? wrapper = encoding switch
                {
                    "gzip" => new GZipStream(current, CompressionMode.Decompress, leaveOpen: true),
                    "br" => new BrotliStream(current, CompressionMode.Decompress, leaveOpen: true),
                    "deflate" => new DeflateStream(current, CompressionMode.Decompress, leaveOpen: true),
                    _ => null
                };

                if (wrapper == null)
                {
                    return null;
                }

                wrappers.Push(wrapper);
                current = wrapper;
            }

            var decoded = new MemoryStream();
            await current.CopyToAsync(decoded);
            decoded.Seek(0, SeekOrigin.Begin);
            return decoded;
        }
        catch (InvalidDataException)
        {
            return null;
        }
        finally
        {
            while (wrappers.Count > 0)
            {
                wrappers.Pop().Dispose();
            }
        }
    }
}
