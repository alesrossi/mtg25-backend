using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace API.Helpers;

public record EventLinkPlayerRow(int Position, string Name, int Score, int Omw, int Gw, int Ogw);

public record EventLinkParseOutput(List<EventLinkPlayerRow> Rows, List<string> Errors, int SkippedLines);

public static partial class EventLinkPdfHelper
{
    // Matches: position(int) + spaces + name(text) + spaces + score(int) + spaces + omw(int) + spaces + gw(int) + spaces + ogw(int)
    // Name is captured as a non-greedy group that ends before the last 4 numeric columns
    [GeneratedRegex(@"(\d{1,3})\s{2,}(.+?)\s{2,}(\d{1,3})\s{2,}(\d{1,3})\s{2,}(\d{1,3})\s{2,}(\d{1,3})")]
    private static partial Regex PlayerPattern();

    public static EventLinkParseOutput ParseEventLinkPdf(Stream pdfStream)
    {
        var rows = new List<EventLinkPlayerRow>();
        var errors = new List<string>();
        var skippedLines = 0;

        using var document = PdfDocument.Open(pdfStream);

        foreach (var page in document.GetPages())
        {
            var text = page.Text;
            var matches = PlayerPattern().Matches(text);

            foreach (Match match in matches)
            {
                try
                {
                    var position = int.Parse(match.Groups[1].Value);
                    var name = match.Groups[2].Value.Trim();
                    var score = int.Parse(match.Groups[3].Value);
                    var omw = int.Parse(match.Groups[4].Value);
                    var gw = int.Parse(match.Groups[5].Value);
                    var ogw = int.Parse(match.Groups[6].Value);

                    rows.Add(new EventLinkPlayerRow(position, name, score, omw, gw, ogw));
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to parse match: '{match.Value}' - {ex.Message}");
                    skippedLines++;
                }
            }
        }

        return new EventLinkParseOutput(rows, errors, skippedLines);
    }
}
