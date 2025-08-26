
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using TestUtilities.Builders;

namespace TestUtilities.Database;

/// <summary>
/// Factory for creating test database contexts with different configurations.
/// Provides isolation between tests and realistic data scenarios.
/// </summary>
public class InMemoryDbContextFactory
{
    /// <summary>
    /// Creates a clean, empty database context for each test.
    /// Each test gets its own isolated database instance.
    /// </summary>
    public static MainContext CreateMain()
    {
        var options = new DbContextOptionsBuilder<MainContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB per test
            .EnableSensitiveDataLogging() // Helpful for debugging test failures
            .Options;

        var context = new MainContext(options);
        context.Database.EnsureCreated(); // Create database schema

        return context;
    }
    
    public static AppIdentityDbContext CreateIdentity()
    {
        var options = new DbContextOptionsBuilder<AppIdentityDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique DB per test
            .EnableSensitiveDataLogging() // Helpful for debugging test failures
            .Options;

        var context = new AppIdentityDbContext(options);
        context.Database.EnsureCreated(); // Create database schema

        return context;
    }

    /// <summary>
    /// Creates a database context pre-populated with common test data.
    /// Useful for tests that need existing data to work with.
    /// </summary>
    public static MainContext CreateWithSeedData()
    {
        var mainContext = CreateMain();
        var identityContext = CreateIdentity();
        SeedTestData(mainContext, identityContext);
        return mainContext;
    }

    /// <summary>
    /// Seeds the test database with realistic, related data.
    /// Demonstrates proper entity relationship setup.
    /// </summary>
    private static void SeedTestData(MainContext context, AppIdentityDbContext identityContext)
    {
        var builder = new TestDataBuilder();
        
        // Create test users - foundation for all other entities
        var user1 = builder.CreateUser("test1@example.com", "testuser1");
        var user2 = builder.CreateUser("test2@example.com", "testuser2");
        
        identityContext.Users.AddRange(user1, user2);
        identityContext.SaveChanges(); // Save users first to get IDs

        // Create collections for each user
        var collection1 = builder.CreateCollection(user1.Id);
        collection1.Name = "User1's Vintage Collection";
        
        var collection2 = builder.CreateCollection(user2.Id);
        collection2.Name = "User2's Modern Collection";
        
        context.Collections.AddRange(collection1, collection2);

        // Create decks for each user
        var deck1 = builder.CreateDeck(user1.Id, "Vintage");
        deck1.Name = "Power Nine Control";
        
        var deck2 = builder.CreateDeck(user2.Id, "Modern");
        deck2.Name = "Burn";
        
        context.Decks.AddRange(deck1, deck2);

        context.SaveChanges(); // Save collections to get IDs
        
        // Create a shared pool of cards
        var cards = new[]
        {
            builder.CreateCard(collection1.Id, "Lightning Bolt", 2.50),
            builder.CreateCard(collection1.Id, "Black Lotus", 15000.00),
            builder.CreateCard(collection2.Id, "Counterspell", 1.25),
            builder.CreateCard(collection2.Id, "Sol Ring", 3.75),
            builder.CreateCard(collection2.Id, "Swords to Plowshares", 4.00)
        };
        
        context.Cards.AddRange(cards);
        
        // Save all changes
        context.SaveChanges();

        // Create relationships between collections and cards
        // (This would typically be done through your domain services)
        // var collectionCard1 = new CollectionCard
        // {
        //     CollectionId = collection1.Id,
        //     CardId = cards[1].Id, // Black Lotus in vintage collection
        //     Quantity = 1
        // };
        //
        // var collectionCard2 = new CollectionCard
        // {
        //     CollectionId = collection2.Id,
        //     CardId = cards.Id, // Lightning Bolt in modern collection
        //     Quantity = 4
        // };
        //
        // context.CollectionCards.AddRange(collectionCard1, collectionCard2);
        // context.SaveChanges();
    }
}
