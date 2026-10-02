using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using API.Dtos.Cards;
using API.Dtos.Collections;

namespace API.Scryfall;

public static class ScryfallUtility
{
    private const string JsonExtension = ".json";
    private const string JsonlExtension = ".jsonl";

    public static async IAsyncEnumerable<ScryfallCardDto> FetchCardListStreamAsync(
        string bulkBasePath,
        string endpoint,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var resolvedBasePath = ResolveBasePath(bulkBasePath);
        var filePath = await GetScryfallBulkDataAsync(resolvedBasePath, endpoint, cancellationToken);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };

        await foreach (var card in DeserializeWithRecoveryAsync(
                           filePath,
                           resolvedBasePath,
                           resolvedBasePath,
                           options,
                           cancellationToken))
        {
            yield return card;
        }
    }

    private static async IAsyncEnumerable<ScryfallCardDto> DeserializeWithRecoveryAsync(
        string primaryPath,
        string resolvedBasePath,
        string bulkBasePath,
        JsonSerializerOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var currentPath = primaryPath;
        var attemptedFallback = false;
        var fallbackPath = FindFallbackDataFile(resolvedBasePath, primaryPath);

        while (true)
        {
            await using var stream = OpenBulkFileStream(currentPath, bulkBasePath);
            var asyncEnumerable = IsJsonl(currentPath)
                ? DeserializeJsonLinesAsync(stream, options, cancellationToken)
                : JsonSerializer.DeserializeAsyncEnumerable<ScryfallCardDto>(stream, options, cancellationToken);
            await using var enumerator = asyncEnumerable.GetAsyncEnumerator(cancellationToken);

            bool restartWithFallback;

            while (true)
            {
                bool moveNext;
                try
                {
                    moveNext = await enumerator.MoveNextAsync();
                }
                catch (JsonException jsonException)
                {
                    if (!attemptedFallback && fallbackPath is not null)
                    {
                        Console.WriteLine($@"Failed to parse Scryfall data file '{currentPath}' ({jsonException.Message}).");

                        if (string.Equals(currentPath, primaryPath, StringComparison.OrdinalIgnoreCase))
                        {
                            TryDeleteCorruptedFile(currentPath);
                        }

                        Console.WriteLine($@"Attempting to load fallback Scryfall data file '{fallbackPath}'.");
                        currentPath = fallbackPath;
                        attemptedFallback = true;
                        restartWithFallback = true;
                        break;
                    }

                    throw new InvalidOperationException($"Failed to parse Scryfall data file '{currentPath}'.", jsonException);
                }

                if (!moveNext)
                {
                    yield break;
                }

                var card = enumerator.Current;
                if (card is not null)
                {
                    yield return card;
                }
            }

            if (restartWithFallback)
            {
            }
        }
    }

    private static async IAsyncEnumerable<ScryfallCardDto?> DeserializeJsonLinesAsync(
        Stream stream,
        JsonSerializerOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            yield return JsonSerializer.Deserialize<ScryfallCardDto>(line, options);
        }
    }

    private static bool IsJsonl(string filePath) =>
        string.Equals(Path.GetExtension(filePath), JsonlExtension, StringComparison.OrdinalIgnoreCase);

    private static void TryDeleteCorruptedFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Swallow IO exceptions – the file will be retried on next startup even if delete fails.
        }
    }

    private static string? FindFallbackDataFile(string resolvedBasePath, string currentPath)
    {
        var currentDate = TryParseDateFromFileName(currentPath);

        var previousFile = Directory
            .EnumerateFiles(resolvedBasePath, "*.json*", SearchOption.TopDirectoryOnly)
            .Where(candidate => Path.GetExtension(candidate) is JsonExtension or JsonlExtension)
            .Where(candidate => !string.Equals(candidate, currentPath, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => new { candidate, date = TryParseDateFromFileName(candidate) })
            .Where(tuple => tuple.date is not null && (currentDate is null || tuple.date < currentDate))
            .OrderByDescending(tuple => tuple.date)
            .Select(tuple => tuple.candidate)
            .FirstOrDefault();

        if (previousFile is not null)
        {
            return previousFile;
        }

        return BuildFallbackFilePath(resolvedBasePath);
    }

    private static DateTime? TryParseDateFromFileName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        return DateTime.TryParseExact(
            fileName,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }
    
    private static async Task<string> GetScryfallBulkDataAsync(
        string resolvedBasePath,
        string endpoint,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(resolvedBasePath);

        var todayPath = FindExistingBulkFile(resolvedBasePath, DateTime.Now);
        if (todayPath is not null)
        {
            return todayPath;
        }

        var fallbackPath = BuildFallbackFilePath(resolvedBasePath);

        try
        {
            var sfClient = GetClient(endpoint);
            var response = await sfClient.GetAsync("bulk-data/default_cards", cancellationToken);
            response.EnsureSuccessStatusCode();

            var bulkDto = await response.Content.ReadFromJsonAsync<BulkDto>(cancellationToken: cancellationToken);
            if (bulkDto is null)
            {
                throw new InvalidOperationException("BulkDto is null");
            }

            var downloadUri = bulkDto.JsonlDownloadUri ?? bulkDto.DownloadUri
                ?? throw new InvalidOperationException("Scryfall bulk data response has no download URI");
            var extension = bulkDto.JsonlDownloadUri is not null ? JsonlExtension : JsonExtension;
            var destinationPath = Path.Combine(resolvedBasePath, DateTime.Now.ToString("yyyyMMdd") + extension);
            var tempPath = destinationPath + ".download";

            using var fileClient = new HttpClient();
            var responseFile = await fileClient.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            responseFile.EnsureSuccessStatusCode();

            // Download to a temp file first so an interrupted download never
            // leaves a truncated file that looks like today's data.
            await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await using var stream = await responseFile.Content.ReadAsStreamAsync(cancellationToken);
                await using var source = new Uri(downloadUri).AbsolutePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                    ? new GZipStream(stream, CompressionMode.Decompress)
                    : stream;
                await source.CopyToAsync(fileStream, cancellationToken);
            }

            File.Move(tempPath, destinationPath, overwrite: true);

            Console.WriteLine($@"File downloaded successfully to {destinationPath}");

            return destinationPath;
        }
        catch (Exception ex)
        {
            if (fallbackPath is not null)
            {
                Console.WriteLine($@"Failed to download latest Scryfall data ({ex.Message}). Using fallback file {fallbackPath}.");
                return fallbackPath;
            }

            throw new InvalidOperationException("Unable to download Scryfall bulk data and no fallback file is available.", ex);
        }
    }

    private static FileStream OpenBulkFileStream(string primaryPath, string basePath)
    {
        try
        {
            return File.OpenRead(primaryPath);
        }
        catch (Exception primaryException)
        {
            var resolvedBasePath = ResolveBasePath(basePath);
            var fallbackPath = BuildFallbackFilePath(resolvedBasePath);

            if (fallbackPath is not null && !string.Equals(primaryPath, fallbackPath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Console.WriteLine($@"Failed to read bulk file '{primaryPath}'. Falling back to '{fallbackPath}'.");
                    return File.OpenRead(fallbackPath);
                }
                catch (Exception fallbackException)
                {
                    throw new InvalidOperationException($"Failed to read fallback Scryfall data file '{fallbackPath}'.", fallbackException);
                }
            }

            throw new InvalidOperationException($"Failed to read Scryfall data file '{primaryPath}'.", primaryException);
        }
    }

    private static string ResolveBasePath(string basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return Path.Combine(AppContext.BaseDirectory, "bulk-data");
        }

        if (Path.IsPathRooted(basePath))
        {
            return basePath;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, basePath));
    }

    private static string? BuildFallbackFilePath(string resolvedBasePath) =>
        FindExistingBulkFile(resolvedBasePath, DateTime.Now.AddDays(-1));

    private static string? FindExistingBulkFile(string resolvedBasePath, DateTime date)
    {
        var baseName = Path.Combine(resolvedBasePath, date.ToString("yyyyMMdd"));
        return new[] { baseName + JsonlExtension, baseName + JsonExtension }.FirstOrDefault(File.Exists);
    }
    
    private static HttpClient GetClient(string endpoint)
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri(endpoint);
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PostmanRuntime/7.29.0"); // Example: mimic Postman
        
        return client;
    }
}
