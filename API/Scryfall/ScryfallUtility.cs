using System.Net.Http.Headers;
using System.Text.Json;
using API.Dtos.Cards;
using API.Dtos.Collections;

namespace API.Scryfall;

public static class ScryfallUtility
{
    public static async Task<List<OracleCardDto>> FetchCardListObjectAsync(string bulkBasePath, string endpoint)
    {
        var filePath = await GetOracleBulkDataAsync(bulkBasePath, endpoint);

        var fileContents = await File.ReadAllTextAsync(filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };
        return JsonSerializer.Deserialize<List<OracleCardDto>>(fileContents, options)!;
    }
    
    private static async Task<string> GetOracleBulkDataAsync(string basePath, string endpoint)
    {
        var resolvedBasePath = ResolveBasePath(basePath);
        Directory.CreateDirectory(resolvedBasePath);

        var fileName = DateTime.Now.ToString("yyyyMMdd") + ".json";
        var destinationPath = Path.Combine(resolvedBasePath, fileName);

        if (File.Exists(destinationPath))
        {
            return destinationPath;
        }

        var sfClient = GetClient(endpoint);
        var response = await sfClient.GetAsync(
            "bulk-data/default_cards");
        if (!response.IsSuccessStatusCode) throw new Exception("Failed to get bulk data");
        var bulkDto = await response.Content.ReadFromJsonAsync<BulkDto>();
        if (bulkDto is null) throw new Exception("BulkDto is null");
            
            
        using var fileClient = new HttpClient();
        var responseFile = await fileClient.GetAsync(bulkDto.DownloadUri, HttpCompletionOption.ResponseHeadersRead);

        if (responseFile.IsSuccessStatusCode)
        {
            // Open a stream to the destination file
            await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

            // Open the stream from the HTTP response
            await using var stream = await responseFile.Content.ReadAsStreamAsync();

            // Copy the response stream into the destination file stream
            await stream.CopyToAsync(fileStream);

            Console.WriteLine($"File downloaded successfully to {destinationPath}");
        }
        else
        {
            throw new Exception($"Failed to download file. HTTP Status: {response.StatusCode}");
        }

        return destinationPath;
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
