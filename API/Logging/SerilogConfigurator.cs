using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Serilog;
using Serilog.Debugging;
using Serilog.Sinks.Elasticsearch;

namespace API.Logging;

public static class SerilogConfigurator
{
    public static void ConfigureElasticsearchLogging(IConfiguration configuration, LoggerConfiguration loggerConfiguration)
    {
        Console.Error.WriteLine("Elasticsearch logging init: start");
        var section = configuration.GetSection("ElasticsearchLogging");
        var nodeUris = section["NodeUris"];
        if (string.IsNullOrWhiteSpace(nodeUris))
        {
            Console.Error.WriteLine("Elasticsearch logging init: missing ElasticsearchLogging:NodeUris");
            Console.Error.WriteLine("Elasticsearch logging disabled: missing ElasticsearchLogging:NodeUris");
            return;
        }

        Console.Error.WriteLine($"Elasticsearch logging init: NodeUris={nodeUris}");
        var sinkOptions = new ElasticsearchSinkOptions(new Uri(nodeUris))
        {
            IndexFormat = section["IndexFormat"],
            AutoRegisterTemplate = section.GetValue("AutoRegisterTemplate", true),
            EmitEventFailure = ParseEmitEventFailure(section["EmitEventFailure"]) | EmitEventFailureHandling.RaiseCallback,
            FailureCallback = (logEvent, exception) =>
            {
                var rendered = logEvent?.RenderMessage() ?? "<null>";
                var error = exception?.Message ?? "<no exception>";
                Console.Error.WriteLine($"Elasticsearch sink failure: {rendered} | {error}");
            }
        };

        var templateVersion = section["AutoRegisterTemplateVersion"];
        if (!string.IsNullOrWhiteSpace(templateVersion) &&
            Enum.TryParse(templateVersion, ignoreCase: true, out AutoRegisterTemplateVersion parsedVersion))
        {
            sinkOptions.AutoRegisterTemplateVersion = parsedVersion;
        }

        var username = section["Username"];
        var password = section["Password"];
        Console.Error.WriteLine($"Elasticsearch logging enabled: nodeUris={nodeUris}, auth={(string.IsNullOrWhiteSpace(username) ? "none" : "basic")}");
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Elasticsearch logging init: basic auth configured");
            sinkOptions.ModifyConnectionSettings = connection =>
                connection.BasicAuthentication(username, password);
        }

        var caPath = section["CaCertificatePath"];
        if (!string.IsNullOrWhiteSpace(caPath))
        {
            Console.Error.WriteLine($"Elasticsearch logging init: CA path configured at {caPath}");
            var existing = sinkOptions.ModifyConnectionSettings;
            sinkOptions.ModifyConnectionSettings = connection =>
            {
                if (existing != null)
                {
                    connection = existing(connection);
                }

                return connection.ServerCertificateValidationCallback(
                    (_, certificate, _, errors) => ValidateElasticCertificate(certificate, caPath, errors));
            };
            Console.Error.WriteLine($"Elasticsearch logging CA path: {caPath} exists={File.Exists(caPath)}");
        }

        loggerConfiguration.WriteTo.Elasticsearch(sinkOptions);
        Console.Error.WriteLine("Elasticsearch logging init: sink configured");
    }

    public static void EnableSerilogSelfLog(IConfiguration configuration)
    {
        Console.Error.WriteLine("SerilogSelfLog init: checking configuration");
        var enabled = configuration.GetValue("SerilogSelfLog:Enabled", false);
        if (!enabled)
        {
            Console.Error.WriteLine("SerilogSelfLog init: disabled");
            return;
        }

        SelfLog.Enable(message => Console.Error.WriteLine($"SerilogSelfLog: {message}"));
        Console.Error.WriteLine("SerilogSelfLog init: enabled");
    }

    private static EmitEventFailureHandling ParseEmitEventFailure(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return EmitEventFailureHandling.WriteToSelfLog;
        }

        return Enum.TryParse(value, ignoreCase: true, out EmitEventFailureHandling parsed)
            ? parsed
            : EmitEventFailureHandling.WriteToSelfLog;
    }

    private static bool ValidateElasticCertificate(X509Certificate? certificate, string caPath, SslPolicyErrors errors)
    {
        if (certificate == null || !File.Exists(caPath))
        {
            return false;
        }

        if (errors == SslPolicyErrors.None)
        {
            return true;
        }

        var caCertificate = X509CertificateLoader.LoadCertificateFromFile(caPath);
        var serverCertificate = certificate as X509Certificate2 ?? new X509Certificate2(certificate);

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        chain.ChainPolicy.ExtraStore.Add(caCertificate);

        if (!chain.Build(serverCertificate))
        {
            return false;
        }

        var root = chain.ChainElements[^1].Certificate;
        return string.Equals(root.Thumbprint, caCertificate.Thumbprint, StringComparison.OrdinalIgnoreCase);
    }
}
