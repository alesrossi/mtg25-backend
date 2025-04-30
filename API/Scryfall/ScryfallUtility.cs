using System.Net.Http.Headers;
using System.Text.Json;
using API.Configuration;
using API.Dtos;

namespace API.Scryfall;

public static class ScryfallUtility
{
    public static async Task<Dictionary<string, OracleCardDto>> FetchCardListObjectAsync(string bulkBasePath, string endpoint)
    {
        //var filePath = "/home/dev/programming/dotnet/mtg25-backend/bulk-data/today.json";
        var filePath = await GetOracleBulkDataAsync(bulkBasePath, endpoint);

        var fileContents = await File.ReadAllTextAsync(filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };
        var obj = JsonSerializer.Deserialize<List<OracleCardDto>>(fileContents, options)!;

        var dict = obj.ToDictionary(x => x.Id);
        return dict;
    }
    
    private static async Task<string> GetOracleBulkDataAsync(string basePath, string endpoint)
    {
        var destinationPath = Path.Combine(basePath, DateTime.Now.ToString("yyyyMMdd") + ".json");
        var sfClient = GetClient(endpoint);
        
        var response = await sfClient.GetAsync(
            "bulk-data/oracle-cards");
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