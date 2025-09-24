using System;
using FluentAssertions;
using Core.Models;
using TestUtilities.Builders;

namespace UnitTests.Models;

public class WishlistTests
{
    private readonly TestDataBuilder _testDataBuilder = new();

    [Fact]
    public void Wishlist_WhenCreated_HasExpectedDefaults()
    {
        var ownerId = "test-owner";
        var wishlist = _testDataBuilder.CreateWishlist(ownerId, isPublic: false);

        wishlist.Id.Should().Be(0, "because new wishlist entities are not persisted yet");
        wishlist.OwnerId.Should().Be(ownerId);
        wishlist.IsPublic.Should().BeFalse();
        wishlist.WishlistCards.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Wishlist_PrivacyFlag_CanBePublic()
    {
        var wishlist = _testDataBuilder.CreateWishlist("owner", isPublic: true);

        wishlist.IsPublic.Should().BeTrue();
    }

    [Fact]
    public void Wishlist_WishlistCardsCollection_AllowsAddingCards()
    {
        var wishlist = _testDataBuilder.CreateWishlist("owner");
        var card = new WishlistCard
        {
            WishlistId = wishlist.Id,
            OracleId = Guid.NewGuid().ToString(),
            Name = "Lightning Bolt",
            SetCode = "LEA",
            DesiredQuantity = 2
        };

        wishlist.WishlistCards.Add(card);

        wishlist.WishlistCards.Should().HaveCount(1);
        wishlist.WishlistCards.First().Name.Should().Be("Lightning Bolt");
    }
}

public class WishlistCardTests
{
    private readonly TestDataBuilder _testDataBuilder = new();

    [Fact]
    public void WishlistCard_WhenCreated_HasExpectedDefaults()
    {
        var wishlist = _testDataBuilder.CreateWishlist("owner");

        var wishlistCard = _testDataBuilder.CreateWishlistCard(wishlist.Id, name: "Counterspell");

        wishlistCard.Id.Should().Be(0, "because wishlist cards are not yet persisted");
        wishlistCard.WishlistId.Should().Be(wishlist.Id);
        wishlistCard.Name.Should().Be("Counterspell");
        wishlistCard.DesiredQuantity.Should().BeGreaterThan(0);
    }

    [Fact]
    public void WishlistCard_CanStoreDisplayDetails()
    {
        var wishlistCard = new WishlistCard
        {
            WishlistId = 1,
            OracleId = Guid.NewGuid().ToString(),
            Name = "Sol Ring",
            SetCode = "CMM",
            SetName = "Commander Masters",
            ImageUrl = "https://example.com/card.png",
            CollectorNumber = "123",
            Rarity = "Rare",
            DesiredQuantity = 1,
            IsFoil = true,
            Language = "English",
            Notes = "Prefer etched foil"
        };

        wishlistCard.SetName.Should().Be("Commander Masters");
        wishlistCard.ImageUrl.Should().Contain("example.com");
        wishlistCard.IsFoil.Should().BeTrue();
        wishlistCard.Notes.Should().Be("Prefer etched foil");
    }
}
