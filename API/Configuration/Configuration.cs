namespace API.Configuration;

public class ScryfallConfig
{
    public string BasePath { get; init; } = string.Empty;
}

public class PathsConfig
{
    public string Bulk { get; init; } = string.Empty;
}

public class RequestLoggingOptions
{
    public const string DefaultCorrelationHeaderName = "X-Correlation-ID";

    public string CorrelationHeaderName { get; init; } = DefaultCorrelationHeaderName;

    public int SlowRequestThresholdMs { get; init; } = 2000;
}
