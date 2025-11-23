using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace TestUtilities.Serialization;

public static class JsonContentHelper
{
    public static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static StringContent CreateContent<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, DefaultOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
