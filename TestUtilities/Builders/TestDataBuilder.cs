using System;
using AutoFixture;
using Core.Models;
using Core.Models.Identity;

namespace TestUtilities.Builders;

/// <summary>
/// Centralized test data creation using the Builder pattern.
/// Provides consistent, realistic test data across all test projects.
/// </summary>
public class TestDataBuilder
{
    private readonly IFixture _fixture;
    private readonly Random _random;

    public TestDataBuilder()
    {
        _fixture = new Fixture();
        _random = new Random();
        
        // Prevent infinite recursion when AutoFixture encounters circular references
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>()
            .ToList().ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    /// <summary>
    /// Creates a test user with optional overrides for specific properties.
    /// Uses realistic data generation while allowing customization.
    /// </summary>
    public AppUser CreateUser(string? email = null, string? userName = null)
    {
        var user = _fixture.Build<AppUser>()
            .With(u => u.Email, email ?? _fixture.Create<string>() + "@test.com")
            .With(u => u.UserName, userName ?? _fixture.Create<string>())
            .With(u => u.EmailConfirmed, true)
            .With(u => u.LockoutEnd, DateTime.UtcNow)
            .Without(u => u.UserLeagues)
            .Without(u => u.Id)  // Let Identity generate the ID
            .Create();
        
        return user;
    }

    /// <summary>
    /// Creates a collection belonging to a specific user.
    /// Demonstrates how to create related entities with proper foreign key relationships.
    /// </summary>
    public Collection CreateCollection(string userId)
    {
        return _fixture.Build<Collection>()
            .With(c => c.OwnerId, userId)
            .With(c => c.NumberOfCards, 0)
            .Without(c => c.Id) // Let Entity Framework generate the ID
            .Create();
    }

    /// <summary>
    /// Creates a deck with realistic MTG formats and data.
    /// Shows how to create domain-specific test data.
    /// </summary>
    public Deck CreateDeck(string userId, string? format = null)
    {
        var validFormats = new[] { "Standard", "Modern", "Legacy", "Commander", "Pioneer" };
        
        return _fixture.Build<Deck>()
            .With(d => d.OwnerId, userId)
            .With(d => d.Format, format ?? validFormats[_random.Next(validFormats.Length)])
            .Without(d => d.Id)
            .Without(d => d.DeckCards) // Initialize as empty collection
            .Create();
    }

    public Wishlist CreateWishlist(string ownerId, bool? isPublic = null)
    {
        return _fixture.Build<Wishlist>()
            .With(w => w.OwnerId, ownerId)
            .With(w => w.IsPublic, isPublic ?? _random.Next(2) == 0)
            .With(w => w.Name, $"Wishlist {Guid.NewGuid():N}"[..16])
            .Without(w => w.Id)
            .Without(w => w.WishlistCards)
            .Create();
    }

    public WishlistCard CreateWishlistCard(int wishlistId, string? oracleId = null, string? name = null)
    {
        return _fixture.Build<WishlistCard>()
            .With(c => c.WishlistId, wishlistId)
            .With(c => c.OracleId, oracleId ?? _fixture.Create<Guid>().ToString())
            .With(c => c.Name, name ?? $"Card {_random.Next(1, 1000)}")
            .With(c => c.DesiredQuantity, _random.Next(1, 5))
            .With(c => c.IsFoil, _random.Next(10) == 0)
            .Without(c => c.Id)
            .Without(c => c.Wishlist)
            .Create();
    }

    public TradeBinder CreateTradeBinder(string ownerId, bool? isPublic = null)
    {
        return _fixture.Build<TradeBinder>()
            .With(tb => tb.OwnerId, ownerId)
            .With(tb => tb.IsPublic, isPublic ?? _random.Next(2) == 0)
            .With(tb => tb.Name, $"Trade Binder {Guid.NewGuid():N}"[..16])
            .Without(tb => tb.Id)
            .Without(tb => tb.BinderCards)
            .Create();
    }

    public BinderCard CreateBinderCard(
        int tradeBinderId,
        int? cardId = null,
        string? name = null,
        int? quantityToTrade = null)
    {
        return _fixture.Build<BinderCard>()
            .With(bc => bc.TradeBinderId, tradeBinderId)
            .With(bc => bc.CardId, cardId ?? 0)
            .With(bc => bc.Name, name ?? $"Binder Card {_random.Next(1, 1000)}")
            .With(bc => bc.QuantityToTrade, quantityToTrade ?? _random.Next(0, 5))
            .Without(bc => bc.Id)
            .Without(bc => bc.TradeBinder)
            .Without(bc => bc.Card)
            .Create();
    }

    /// <summary>
    /// Creates MTG cards with realistic properties.
    /// Demonstrates creating entities with complex business rules.
    /// </summary>
    public Card CreateCard(int collectionId, string? name = null, double? price = null)
    {
        var cardNames = new[] 
        { 
            "Lightning Bolt", "Black Lotus", "Counterspell", "Sol Ring",
            "Swords to Plowshares", "Path to Exile", "Force of Will"
        };

        return _fixture.Build<Card>()
            .With(c => c.CollectionId, collectionId)
            .With(c => c.Name, name ?? cardNames[_random.Next(cardNames.Length)])
            .With(c => c.PurchasePrice, price ?? _fixture.Create<double>() % 1000)
            .With(c => c.OracleId, _fixture.Create<Guid>().ToString())
            .With(c => c.Quantity, _random.Next(1, 10))
            .With(c => c.Language, "English")
            .With(c => c.Condition, Condition.NearMint)
            .With(c => c.IsFoil, _random.Next(10) == 0) // 10% chance of foil
            .With(c => c.PurchasePriceCurrency, "USD")
            .With(c => c.ImageUrl, $"https://cards.scryfall.io/normal/front/{_fixture.Create<Guid>()}.jpg")
            .With(c => c.BackImageUrl, _random.Next(2) == 0 ? null : $"https://cards.scryfall.io/normal/back/{_fixture.Create<Guid>()}.jpg")
            .With(c => c.SetCode, GetRandomSetCode())
            .With(c => c.SetName, GetRandomSetName())
            .With(c => c.CollectorNumber, _random.Next(1, 400).ToString())
            .With(c => c.Rarity, GetRandomRarity())
            .With(c => c.IsMisprint, false)
            .With(c => c.IsAltered, false)
            .Without(c => c.Id) // Let Entity Framework generate the ID
            .Without(c => c.Collection) // Don't auto-generate the collection navigation property
            .Create();
    }

    /// <summary>
    /// Creates a card with a specific Oracle ID to match deck cards for ownership testing.
    /// </summary>
    public Card CreateCardWithOracleId(int collectionId, string oracleId, string? name = null, int quantity = 1)
    {
        var card = CreateCard(collectionId, name);
        card.OracleId = oracleId;
        card.Quantity = quantity;
        return card;
    }

    /// <summary>
    /// Creates a league with realistic tournament properties.
    /// </summary>
    public League CreateLeague(string ownerId, string? format = null)
    {
        var validFormats = new[] { "Standard", "Modern", "Legacy", "Commander", "Pioneer", "Draft", "Sealed" };
        var leagueCode = GenerateLeagueCode();
        
        return _fixture.Build<League>()
            .With(l => l.OwnerId, ownerId)
            .With(l => l.Format, format ?? validFormats[_random.Next(validFormats.Length)])
            .With(l => l.Code, leagueCode)
            .With(l => l.TotalRounds, _random.Next(4, 9)) // 4-8 rounds
            .With(l => l.RoundsToConsider, _random.Next(3, 6)) // Consider 3-5 rounds
            .With(l => l.MinimumRounds, _random.Next(2, 4)) // Minimum 2-3 rounds
            .With(l => l.TotalPlayers, new[] { 8, 16, 32, 64 }[_random.Next(4)]) // Common tournament sizes
            .With(l => l.PointsToGive, new List<int> { 3, 1, 0 }) // Standard points system
            .With(l => l.IsActive, true)
            .Without(l => l.Id)
            .Without(l => l.UserLeagues)
            .Create();
    }

    private string GetRandomSetCode()
    {
        var setCodes = new[] { "LEA", "LEB", "ARN", "ATQ", "LEG", "DRK", "FEM", "ICE", "HML", "ALL", "MRD", "DST", "5DN" };
        return setCodes[_random.Next(setCodes.Length)];
    }

    private string GetRandomSetName()
    {
        var setNames = new[] 
        { 
            "Limited Edition Alpha", "Limited Edition Beta", "Arabian Nights", "Antiquities", 
            "Legends", "The Dark", "Fallen Empires", "Ice Age", "Homelands", "Alliances",
            "Mirrodin", "Darksteel", "Fifth Dawn"
        };
        return setNames[_random.Next(setNames.Length)];
    }

    private string GetRandomRarity()
    {
        var rarities = new[] { "Common", "Uncommon", "Rare", "Mythic Rare" };
        return rarities[_random.Next(rarities.Length)];
    }

    private string GenerateLeagueCode()
    {
        var prefix = new[] { "STD", "MOD", "LEG", "COM", "PIO", "DFT", "SEA" };
        var suffix = DateTime.UtcNow.Year % 100; // Last two digits of year
        var number = _random.Next(1, 100);
        return $"{prefix[_random.Next(prefix.Length)]}{suffix}{number:D2}";
    }

    /// <summary>
    /// Creates realistic MTG mana costs like "{1}{R}", "{2}{U}{U}", etc.
    /// </summary>
    // private string GenerateManaCost()
    // {
    //     var colors = new[] { "{R}", "{G}", "{U}", "{B}", "{W}" };
    //     var genericCost = _random.Next(8); // 0-7 generic mana
    //     
    //     // Select 0-3 random colors
    //     var colorCount = _random.Next(0, 4);
    //     var selectedColors = new List<string>();
    //     
    //     for (int i = 0; i < colorCount; i++)
    //     {
    //         selectedColors.Add(colors[_random.Next(colors.Length)]);
    //     }
    //     
    //     return $"{{{genericCost}}}" + string.Join("", selectedColors);
    // }
}
