using System.Text.Json.Serialization;

namespace API.Dtos.Collections;



public class BulkDto
{
    public required string Id { get; set; }
    [JsonPropertyName("download_uri")]
    public required string DownloadUri { get; set; }
}