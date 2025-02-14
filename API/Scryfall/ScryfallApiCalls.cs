using System.Net;
using System.Net.Http.Headers;
using API.Dtos;
using Core.Models;

namespace API.Scryfall;

public static class ScryfallApiCalls
{
    private static readonly Uri BaseUrl = new Uri("https://api.scryfall.com/");
    private static HttpClient GetClient()
    {
        var client = new HttpClient();
        client.BaseAddress = BaseUrl;
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PostmanRuntime/7.29.0"); // Example: mimic Postman
        
        return client;
    }


    public static async void GetOracleBulkData()
    {
        var destinationPath = Path.Combine("/home/dev/programming/dotnet/mtg25-backend/bulk-data/today.json");
        
        var sfClient = GetClient();
        var response = await sfClient.GetAsync(
            new Uri(BaseUrl, "bulk-data/oracle-cards").ToString());
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


    }
}