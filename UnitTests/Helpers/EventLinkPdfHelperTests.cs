using API.Helpers;
using FluentAssertions;

namespace UnitTests.Helpers;

public class EventLinkPdfHelperTests
{
    [Fact]
    public void ParseEventLinkPdf_WithValidPdf_ReturnsAllPlayerRows()
    {
        using var stream = File.OpenRead("TestData/finale.pdf");

        var result = EventLinkPdfHelper.ParseEventLinkPdf(stream);

        result.Rows.Should().HaveCount(38);
        result.Errors.Should().BeEmpty();
        result.SkippedLines.Should().Be(0);
    }

    [Fact]
    public void ParseEventLinkPdf_WithValidPdf_ParsesFirstPlayerCorrectly()
    {
        using var stream = File.OpenRead("TestData/finale.pdf");

        var result = EventLinkPdfHelper.ParseEventLinkPdf(stream);

        var first = result.Rows[0];
        first.Position.Should().Be(1);
        first.Name.Should().Be("Fabio Paglieri");
        first.Score.Should().Be(21);
        first.Omw.Should().Be(54);
        first.Gw.Should().Be(73);
        first.Ogw.Should().Be(57);
    }

    [Fact]
    public void ParseEventLinkPdf_WithValidPdf_ParsesLastPlayerCorrectly()
    {
        using var stream = File.OpenRead("TestData/finale.pdf");

        var result = EventLinkPdfHelper.ParseEventLinkPdf(stream);

        var last = result.Rows[^1];
        last.Position.Should().Be(38);
        last.Name.Should().Be("Giorgio Tammetta");
        last.Score.Should().Be(0);
        last.Omw.Should().Be(51);
        last.Gw.Should().Be(33);
        last.Ogw.Should().Be(50);
    }

    [Fact]
    public void ParseEventLinkPdf_WithValidPdf_ParsesNameWithQuotes()
    {
        using var stream = File.OpenRead("TestData/finale.pdf");

        var result = EventLinkPdfHelper.ParseEventLinkPdf(stream);

        var player = result.Rows.FirstOrDefault(r => r.Position == 15);
        player.Should().NotBeNull();
        player!.Name.Should().Contain("Zadok");
    }

    [Fact]
    public void ParseEventLinkPdf_WithValidPdf_ParsesNameWithApostrophe()
    {
        using var stream = File.OpenRead("TestData/finale.pdf");

        var result = EventLinkPdfHelper.ParseEventLinkPdf(stream);

        var player = result.Rows.FirstOrDefault(r => r.Position == 34);
        player.Should().NotBeNull();
        player!.Name.Should().Contain("D'amore");
    }

    [Fact]
    public void ParseEventLinkPdf_WithValidPdf_ParsesMultiWordNames()
    {
        using var stream = File.OpenRead("TestData/finale.pdf");

        var result = EventLinkPdfHelper.ParseEventLinkPdf(stream);

        var player = result.Rows.FirstOrDefault(r => r.Position == 30);
        player.Should().NotBeNull();
        player!.Name.Should().Be("Marco De Angelis");
    }

    [Fact]
    public void ParseEventLinkPdf_WithValidPdf_MaintainsPositionOrder()
    {
        using var stream = File.OpenRead("TestData/finale.pdf");

        var result = EventLinkPdfHelper.ParseEventLinkPdf(stream);

        var positions = result.Rows.Select(r => r.Position).ToList();
        positions.Should().BeInAscendingOrder();
        positions.Should().StartWith(1);
        positions.Should().EndWith(38);
    }

    [Fact]
    public void ParseEventLinkPdf_WithEmptyStream_ReturnsEmptyResult()
    {
        using var stream = new MemoryStream();

        var act = () => EventLinkPdfHelper.ParseEventLinkPdf(stream);

        act.Should().Throw<Exception>();
    }
}
