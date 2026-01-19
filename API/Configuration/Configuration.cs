namespace API.Configuration;

public class ScryfallConfig
{
    public string BasePath { get; init; } = string.Empty;
}

public class PathsConfig
{
    public string Bulk { get; init; } = string.Empty;
}

public class MinioConfig
{
    public bool Enabled { get; init; }
    public string Endpoint { get; init; } = string.Empty;
    public string Bucket { get; init; } = string.Empty;
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public bool UseSsl { get; init; } = true;
    public string Prefix { get; init; } = string.Empty;
}

public class RequestLoggingOptions
{
    public const string DefaultCorrelationHeaderName = "X-Correlation-ID";

    public string CorrelationHeaderName { get; init; } = DefaultCorrelationHeaderName;

    public int SlowRequestThresholdMs { get; init; } = 2000;

    public bool IncludeRequestBody { get; init; }

    public bool IncludeResponseBody { get; init; }

    public int BodySizeLimitKb { get; init; } = 128;
}
