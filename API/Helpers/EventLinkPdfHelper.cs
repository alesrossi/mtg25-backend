using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace API.Helpers;

public record EventLinkPlayerRow(int Position, string Name, int Score, int Omw, int Gw, int Ogw);

public record EventLinkParseOutput(List<EventLinkPlayerRow> Rows, List<string> Errors, int SkippedLines);

public static partial class EventLinkPdfHelper
{
    [GeneratedRegex(@"^\d+\s+.+\s+\d+\s+\d+\s+\d+\s+\d+$")]
    private static partial Regex PlayerLinePattern();

    public static EventLinkParseOutput ParseEventLinkPdf(Stream pdfStream)
    {
        var rows = new List<EventLinkPlayerRow>();
        var errors = new List<string>();
        var skippedLines = 0;

        using var document = PdfDocument.Open(pdfStream);

        foreach (var page in document.GetPages())
        {
            var text = page.Text;
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (!PlayerLinePattern().IsMatch(line))
                    continue;

                try
                {
                    var row = ParsePlayerLine(line);
                    rows.Add(row);
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to parse line: '{line}' - {ex.Message}");
                    skippedLines++;
                }
            }
        }

        return new EventLinkParseOutput(rows, errors, skippedLines);
    }

    private static EventLinkPlayerRow ParsePlayerLine(string line)
    {
        // Lines look like: "1 Fabio Paglieri 21 54 73 57"
        // Split from the end to extract numeric columns, remainder is position + name
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 6)
            throw new FormatException("Line has fewer than 6 space-separated tokens");

        var ogw = int.Parse(parts[^1]);
        var gw = int.Parse(parts[^2]);
        var omw = int.Parse(parts[^3]);
        var score = int.Parse(parts[^4]);
        var position = int.Parse(parts[0]);

        // Name is everything between position and the 4 numeric columns at the end
        var name = string.Join(' ', parts[1..^4]);

        return new EventLinkPlayerRow(position, name, score, omw, gw, ogw);
    }
}
