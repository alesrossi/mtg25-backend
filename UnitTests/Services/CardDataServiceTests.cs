using API.Dtos.Cards;
using API.Services;
using FluentAssertions;
using TestUtilities.Builders;

namespace UnitTests.Services;

public class CardDataServiceTests
{
    private readonly TestDataBuilder _builder = new();

    [Fact]
    public void ResolveMainFaceManaCost_WithNullCard_ReturnsNull()
    {
        CardDataService.ResolveMainFaceManaCost(null).Should().BeNull();
    }

    [Fact]
    public void ResolveMainFaceManaCost_WithSingleFaceCard_ReturnsManaCost()
    {
        var card = _builder.CreateOracleCard(name: "Lightning Bolt") with { ManaCost = "{R}" };

        CardDataService.ResolveMainFaceManaCost(card).Should().Be("{R}");
    }

    [Fact]
    public void ResolveMainFaceManaCost_WithSingleFaceCard_EmptyManaCost_ReturnsNull()
    {
        var card = _builder.CreateOracleCard(name: "Island") with { ManaCost = "" };

        CardDataService.ResolveMainFaceManaCost(card).Should().BeNull();
    }

    [Fact]
    public void ResolveMainFaceManaCost_WithMultiFaceCard_ReturnsFirstFaceManaCost()
    {
        var imageUris = new ImageUris("s", "n", "l", "p", "a", "b");
        var cardFace = new CardFace("card_face", "Front", "{1}{R}", "Sorcery", "", [], "", "", "", "", imageUris);
        var card = _builder.CreateOracleCard(name: "Split Card") with
        {
            ManaCost = (string?)null,
            CardFaces = [cardFace]
        };

        CardDataService.ResolveMainFaceManaCost(card).Should().Be("{1}{R}");
    }

    [Fact]
    public void ResolveMainFaceManaCost_WithWhitespaceOnly_ReturnsNull()
    {
        var card = _builder.CreateOracleCard(name: "Test") with { ManaCost = "   " };

        CardDataService.ResolveMainFaceManaCost(card).Should().BeNull();
    }
}
