using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using API.Configuration;
using API.Dtos.Cards;
using API.Dtos.Collections;

namespace API.Scryfall;

public static class ScryfallUtility
{
    public static async IAsyncEnumerable<ScryfallCardDto> FetchCardListStreamAsync(
        string bulkBasePath,
        string endpoint,
        MinioConfig? minioConfig,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var useMinio = minioConfig?.Enabled == true;
        var resolvedBasePath = ResolveBasePath(bulkBasePath, useMinio);
        var filePath = await GetScryfallBulkDataAsync(resolvedBasePath, endpoint, minioConfig, cancellationToken);

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
            var asyncEnumerable = JsonSerializer.DeserializeAsyncEnumerable<ScryfallCardDto>(stream, options, cancellationToken);
            await using var enumerator = asyncEnumerable.GetAsyncEnumerator(cancellationToken);

            var restartWithFallback = false;

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
                        Console.WriteLine($"Failed to parse Scryfall data file '{currentPath}' ({jsonException.Message}).");

                        if (string.Equals(currentPath, primaryPath, StringComparison.OrdinalIgnoreCase))
                        {
                            TryDeleteCorruptedFile(currentPath);
                        }

                        Console.WriteLine($"Attempting to load fallback Scryfall data file '{fallbackPath}'.");
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
                continue;
            }
        }
    }

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
            .EnumerateFiles(resolvedBasePath, "*.json", SearchOption.TopDirectoryOnly)
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

        var previousDayPath = BuildFallbackFilePath(resolvedBasePath);
        return File.Exists(previousDayPath)
            ? previousDayPath
            : null;
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
        MinioConfig? minioConfig,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(resolvedBasePath);

        var fileName = DateTime.Now.ToString("yyyyMMdd") + ".json";
        var destinationPath = Path.Combine(resolvedBasePath, fileName);
        var fallbackPath = BuildFallbackFilePath(resolvedBasePath);

        if (File.Exists(destinationPath))
        {
            return destinationPath;
        }

        if (minioConfig?.Enabled == true)
        {
            var minioKey = BuildMinioObjectKey(minioConfig, fileName);
            using var s3 = CreateS3Client(minioConfig);

            if (await TryDownloadFromMinioAsync(s3, minioConfig, minioKey, destinationPath, cancellationToken))
            {
                Console.WriteLine($"Loaded Scryfall data from MinIO: {minioKey}");
                return destinationPath;
            }
        }

        try
        {
            var sfClient = GetClient(endpoint);
            var response = await sfClient.GetAsync("bulk-data/default_cards");
            response.EnsureSuccessStatusCode();

            var bulkDto = await response.Content.ReadFromJsonAsync<BulkDto>();
            if (bulkDto is null)
            {
                throw new InvalidOperationException("BulkDto is null");
            }

            using var fileClient = new HttpClient();
            var responseFile = await fileClient.GetAsync(bulkDto.DownloadUri, HttpCompletionOption.ResponseHeadersRead);
            responseFile.EnsureSuccessStatusCode();

            await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var stream = await responseFile.Content.ReadAsStreamAsync();
            await stream.CopyToAsync(fileStream, cancellationToken);

            Console.WriteLine($"File downloaded successfully to {destinationPath}");

            if (minioConfig?.Enabled == true)
            {
                var minioKey = BuildMinioObjectKey(minioConfig, fileName);
                using var s3 = CreateS3Client(minioConfig);
                await UploadToMinioAsync(s3, minioConfig, minioKey, destinationPath, cancellationToken);
            }

            return destinationPath;
        }
        catch (Exception ex)
        {
            if (minioConfig?.Enabled == true)
            {
                var fallbackKey = BuildMinioObjectKey(minioConfig, Path.GetFileName(fallbackPath));
                using var s3 = CreateS3Client(minioConfig);
                if (await TryDownloadFromMinioAsync(s3, minioConfig, fallbackKey, fallbackPath, cancellationToken))
                {
                    Console.WriteLine($"Failed to download latest Scryfall data ({ex.Message}). Using MinIO fallback {fallbackKey}.");
                    return fallbackPath;
                }
            }

            if (!string.IsNullOrWhiteSpace(fallbackPath) && File.Exists(fallbackPath))
            {
                Console.WriteLine($"Failed to download latest Scryfall data ({ex.Message}). Using fallback file {fallbackPath}.");
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
            var resolvedBasePath = ResolveBasePath(basePath, false);
            var fallbackPath = BuildFallbackFilePath(resolvedBasePath);

            if (!string.IsNullOrWhiteSpace(fallbackPath) && !string.Equals(primaryPath, fallbackPath, StringComparison.OrdinalIgnoreCase) && File.Exists(fallbackPath))
            {
                try
                {
                    Console.WriteLine($"Failed to read bulk file '{primaryPath}'. Falling back to '{fallbackPath}'.");
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

    private static string ResolveBasePath(string basePath, bool useTempCache)
    {
        if (useTempCache)
        {
            return Path.Combine(Path.GetTempPath(), "mtg25-bulk");
        }

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

    private static string BuildMinioObjectKey(MinioConfig minioConfig, string fileName)
    {
        var prefix = minioConfig.Prefix?.Trim('/');
        return string.IsNullOrWhiteSpace(prefix)
            ? fileName
            : $"{prefix}/{fileName}";
    }

    private static IAmazonS3 CreateS3Client(MinioConfig minioConfig)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = minioConfig.Endpoint,
            ForcePathStyle = true,
            RegionEndpoint = RegionEndpoint.USEast1
        };

        config.UseHttp = !minioConfig.UseSsl;

        return new AmazonS3Client(minioConfig.AccessKey, minioConfig.SecretKey, config);
    }

    private static async Task<bool> TryDownloadFromMinioAsync(
        IAmazonS3 s3,
        MinioConfig minioConfig,
        string key,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await s3.GetObjectMetadataAsync(minioConfig.Bucket, key, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        var response = await s3.GetObjectAsync(minioConfig.Bucket, key, cancellationToken);
        await using var responseStream = response.ResponseStream;
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await responseStream.CopyToAsync(fileStream, cancellationToken);
        return true;
    }

    private static async Task UploadToMinioAsync(
        IAmazonS3 s3,
        MinioConfig minioConfig,
        string key,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        var putRequest = new PutObjectRequest
        {
            BucketName = minioConfig.Bucket,
            Key = key,
            FilePath = sourcePath
        };

        await s3.PutObjectAsync(putRequest, cancellationToken);
        Console.WriteLine($"Uploaded Scryfall data to MinIO: {key}");
    }

    private static string BuildFallbackFilePath(string resolvedBasePath)
    {
        var previousDay = DateTime.Now.AddDays(-1).ToString("yyyyMMdd");
        return Path.Combine(resolvedBasePath, previousDay + ".json");
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
