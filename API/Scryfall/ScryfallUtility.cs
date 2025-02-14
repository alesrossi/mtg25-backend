using System.Text.Json;
using API.Dtos;

namespace API.Scryfall;

public static class ScryfallUtility
{
    public static Dictionary<string, OracleCardDto> FetchCardListObject()
    {
        var filePath = "/home/dev/programming/dotnet/mtg25-backend/bulk-data/today.json";
        

        var fileContents = File.ReadAllText(filePath);
        var obj = JsonSerializer.Deserialize<List<OracleCardDto>>(fileContents)!;
        var dict = obj.ToDictionary(x => x.Id);
        return dict;
    }
}