using System.Text.Json.Serialization;

namespace API.Dtos.Collections;



public class BulkDto
{
    public required string Id { get; set; }
    // Scryfall replaced download_uri (plain JSON array) with jsonl_download_uri
    // (gzipped JSON Lines); both are optional so either shape deserializes.
    [JsonPropertyName("download_uri")]
    public string? DownloadUri { get; set; }
    [JsonPropertyName("jsonl_download_uri")]
    public string? JsonlDownloadUri { get; set; }
}