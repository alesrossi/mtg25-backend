namespace API.Helpers;

public static class ProblemResultFactory
{
    private const string ProblemTypeBase = "https://httpstatuses.io/";

    public static IResult Create(HttpContext context, int statusCode, string title, string? detail = null, string? errorCode = null, IDictionary<string, object?>? extensions = null)
    {
        var extensionPayload = extensions is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(extensions, StringComparer.OrdinalIgnoreCase);

        extensionPayload["traceId"] = context.TraceIdentifier;

        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            extensionPayload["errorCode"] = errorCode;
        }

        return Results.Problem(
            detail: detail,
            instance: context.Request.Path,
            statusCode: statusCode,
            title: title,
            type: $"{ProblemTypeBase}{statusCode}",
            extensions: extensionPayload);
    }
}
