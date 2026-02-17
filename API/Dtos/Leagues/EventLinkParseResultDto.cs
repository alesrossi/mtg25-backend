namespace API.Dtos.Leagues;

public record EventLinkParseResultDto(
    List<EventLinkParsedPlayer> Players,
    List<string> Errors,
    int SkippedLines);

public record EventLinkParsedPlayer(
    string UserId,
    string PdfName,
    bool Matched,
    int Position,
    int Score,
    int Omw,
    int Gw,
    int Ogw);
