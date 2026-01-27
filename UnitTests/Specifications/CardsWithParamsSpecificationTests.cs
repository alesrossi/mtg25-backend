using Core.Models;
using Core.Specifications;
using FluentAssertions;
using Core.Enums;

namespace UnitTests.Specifications;

public class CardsWithParamsSpecificationTests
{
    [Fact]
    public void TypeLineFilter_ReturnsCardsMatchingRequestedType()
    {
        var entityParams = new EntitySpecParams { TypeLine = "Creature" };
        var spec = new CardsWithParamsSpecification(entityParams, collectionId: 1);

        var creatureCard = CreateCard(1, "Legendary Creature — Elf Druid");
        var sorceryCard = CreateCard(1, "Sorcery");

        spec.Criteria.Compile()(creatureCard).Should().BeTrue();
        spec.Criteria.Compile()(sorceryCard).Should().BeFalse();
    }

    [Fact]
    public void TypeLineFilter_IgnoresInvalidRequestedTypes()
    {
        var entityParams = new EntitySpecParams { TypeLine = "Vehicle" };
        entityParams.TypeLine.Should().BeNull();

        var spec = new CardsWithParamsSpecification(entityParams, collectionId: 1);
        var artifactCard = CreateCard(1, "Artifact — Equipment");

        spec.Criteria.Compile()(artifactCard).Should().BeTrue("no typeline filter should be applied when an unsupported type is provided");
    }

    [Fact]
    public void SortByTypeLineAscending_UsesTypeLineOrdering()
    {
        var entityParams = new EntitySpecParams { Sort = "typeAsc" };
        var spec = new CardsWithParamsSpecification(entityParams, collectionId: 1);

        spec.OrderBy.Should().NotBeNull();
        spec.OrderBy.Body.ToString().Should().Contain("TypeLine");
    }

    [Fact]
    public void SortByTypeLineDescending_UsesTypeLineOrdering()
    {
        var entityParams = new EntitySpecParams { Sort = "typeDesc" };
        var spec = new CardsWithParamsSpecification(entityParams, collectionId: 1);

        spec.OrderByDescending.Should().NotBeNull();
        spec.OrderByDescending.Body.ToString().Should().Contain("TypeLine");
    }

    private static Card CreateCard(int collectionId, string typeLine)
    {
        return new Card
        {
            CollectionId = collectionId,
            Name = "Test Card",
            ScryfallId = Guid.NewGuid().ToString(),
            Quantity = 1,
            Language = Language.En,
            Condition = Condition.NearMint,
            IsFoil = false,
            PurchasePrice = 1,
            PurchasePriceCurrency = Currency.Usd,
            ImageUrl = "https://example.com/image.png",
            BackImageUrl = null,
            ArtCrop = "https://example.com/art.png",
            SetCode = "SET",
            SetName = "Set Name",
            CollectorNumber = "001",
            Rarity = "Common",
            IsMisprint = false,
            IsAltered = false,
            TypeLine = typeLine
        };
    }
}
