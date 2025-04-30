using System.Text.Json.Serialization;

namespace API.Dtos;



public class BulkDto
{
    private string Id { get; set; }
    [JsonPropertyName("download_uri")]
    public string DownloadUri { get; set; }
}