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
            .With(u => u.LockoutEnd, DateTimeOffset.UtcNow)
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
            .Create();
    }

    /// <summary>
    /// Creates MTG cards with realistic properties.
    /// Demonstrates creating entities with complex business rules.
    /// </summary>
    public Card CreateCard(string? name = null, double? price = null)
    {
        var cardNames = new[] 
        { 
            "Lightning Bolt", "Black Lotus", "Counterspell", "Sol Ring",
            "Swords to Plowshares", "Path to Exile", "Force of Will"
        };

        return _fixture.Build<Card>()
            .With(c => c.Name, name ?? cardNames[_random.Next(cardNames.Length)])
            .With(c => c.PurchasePrice, price ?? _fixture.Create<double>() % 1000)
            .Create();
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
