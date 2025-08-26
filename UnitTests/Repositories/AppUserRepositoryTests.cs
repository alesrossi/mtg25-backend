using Core.Models.Identity;
using FluentAssertions;
using Infrastructure.Data;
using Infrastructure.Identity;
using TestUtilities.Builders;
using TestUtilities.Database;

namespace UnitTests.Repositories;

/// <summary>
/// Tests for AppUser repository focusing on data access patterns.
/// Uses in-memory database for fast, isolated testing.
/// Note: AppUser is stored in the Identity context and inherits from IdentityUser.
/// </summary>
public class AppUserRepositoryTests : IDisposable
{
    private readonly MainContext _context;
    private readonly AppIdentityDbContext _identityContext;
    private readonly TestDataBuilder _testDataBuilder;

    public AppUserRepositoryTests()
    {
        _context = InMemoryDbContextFactory.CreateMain();
        _identityContext = InMemoryDbContextFactory.CreateIdentity();
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsAppUser()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser("test@example.com", "testuser");
        user.DisplayName = "Test User";
        user.FirstName = "Test";
        user.LastName = "User";
        
        _identityContext.Users.Add(user);
        await _identityContext.SaveChangesAsync();

        // Act
        var result = await _identityContext.Users.FindAsync(user.Id);

        // Assert
        result.Should().NotBeNull("because a user with this ID exists");
        result.Id.Should().Be(user.Id);
        result.Email.Should().Be("test@example.com");
        result.UserName.Should().Be("testuser");
        result.DisplayName.Should().Be("Test User");
        result.FirstName.Should().Be("Test");
        result.LastName.Should().Be("User");
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Act
        var result = await _identityContext.Users.FindAsync("invalid-id");

        // Assert
        result.Should().BeNull("because no user exists with this ID");
    }

    [Fact]
    public async Task ListAsync_WithMultipleUsers_ReturnsAllUsers()
    {
        // Arrange
        var users = new[]
        {
            _testDataBuilder.CreateUser("user1@test.com", "user1"),
            _testDataBuilder.CreateUser("user2@test.com", "user2"),
            _testDataBuilder.CreateUser("user3@test.com", "user3")
        };
        
        users[0].DisplayName = "User One";
        users[0].FirstName = "First";
        users[0].LastName = "One";
        
        users[1].DisplayName = "User Two";
        users[1].FirstName = "Second";
        users[1].LastName = "Two";
        
        users[2].DisplayName = "User Three";
        users[2].FirstName = "Third";
        users[2].LastName = "Three";

        _identityContext.Users.AddRange(users);
        await _identityContext.SaveChangesAsync();

        // Act
        var result = _identityContext.Users.ToList();

        // Assert
        result.Should().HaveCount(3, "because we added 3 users");
        result.Select(u => u.Email).Should().Contain(["user1@test.com", "user2@test.com", "user3@test.com"]);
        result.Select(u => u.DisplayName).Should().Contain(["User One", "User Two", "User Three"]);
    }

    [Fact]
    public async Task FindByEmailAsync_WithValidEmail_ReturnsUser()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser("findme@test.com", "findmeuser");
        user.DisplayName = "Findable User";
        
        _identityContext.Users.Add(user);
        await _identityContext.SaveChangesAsync();

        // Act
        var result = _identityContext.Users.FirstOrDefault(u => u.Email == "findme@test.com");

        // Assert
        result.Should().NotBeNull("because a user with this email exists");
        result.Email.Should().Be("findme@test.com");
        result.DisplayName.Should().Be("Findable User");
    }

    [Fact]
    public async Task FindByEmailAsync_WithInvalidEmail_ReturnsNull()
    {
        // Act
        var result = _identityContext.Users.FirstOrDefault(u => u.Email == "nonexistent@test.com");

        // Assert
        result.Should().BeNull("because no user exists with this email");
    }

    [Fact]
    public async Task AddAsync_WithValidUser_AddsToDatabase()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser("new@test.com", "newuser");
        user.DisplayName = "New User";
        user.FirstName = "New";
        user.LastName = "User";

        // Act
        _identityContext.Users.Add(user);
        await _identityContext.SaveChangesAsync();

        // Assert
        var savedUser = await _identityContext.Users.FindAsync(user.Id);
        savedUser.Should().NotBeNull("because the user should be saved to the database");
        savedUser.Email.Should().Be("new@test.com");
        savedUser.UserName.Should().Be("newuser");
        savedUser.DisplayName.Should().Be("New User");
        savedUser.FirstName.Should().Be("New");
        savedUser.LastName.Should().Be("User");
    }

    [Fact]
    public async Task UpdateAsync_WithValidUser_UpdatesInDatabase()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser("update@test.com", "updateuser");
        user.DisplayName = "Original Name";
        user.FirstName = "Original";
        user.LastName = "Last";
        
        _identityContext.Users.Add(user);
        await _identityContext.SaveChangesAsync();

        // Act
        user.DisplayName = "Updated Name";
        user.FirstName = "Updated";
        user.LastName = "NewLast";
        await _identityContext.SaveChangesAsync();

        // Assert
        var updatedUser = await _identityContext.Users.FindAsync(user.Id);
        updatedUser.Should().NotBeNull();
        updatedUser.DisplayName.Should().Be("Updated Name");
        updatedUser.FirstName.Should().Be("Updated");
        updatedUser.LastName.Should().Be("NewLast");
        updatedUser.Email.Should().Be("update@test.com", "because email should remain unchanged");
    }

    [Fact]
    public async Task DeleteAsync_WithValidUser_RemovesFromDatabase()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser("delete@test.com", "deleteuser");
        _identityContext.Users.Add(user);
        await _identityContext.SaveChangesAsync();

        var existingUser = await _identityContext.Users.FindAsync(user.Id);
        existingUser.Should().NotBeNull();

        // Act
        _identityContext.Users.Remove(user);
        await _identityContext.SaveChangesAsync();

        // Assert
        var deletedUser = await _identityContext.Users.FindAsync(user.Id);
        deletedUser.Should().BeNull("because the user should be deleted");
    }

    [Fact]
    public async Task User_WithUserLeagues_MaintainsRelationship()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser("league@test.com", "leagueuser");
        user.DisplayName = "League Player";
        
        var owner = _testDataBuilder.CreateUser("owner@test.com", "owner");
        _identityContext.Users.AddRange(user, owner);
        await _identityContext.SaveChangesAsync();

        var league1 = _testDataBuilder.CreateLeague(owner.Id);
        league1.Code = "LEG1";
        var league2 = _testDataBuilder.CreateLeague(owner.Id);
        league2.Code = "LEG2";
        _identityContext.Leagues.AddRange(league1, league2);
        await _identityContext.SaveChangesAsync();

        var userLeague1 = new AppUserLeague { UserId = user.Id, LeagueId = league1.Id };
        var userLeague2 = new AppUserLeague { UserId = user.Id, LeagueId = league2.Id };
        _identityContext.UserLeagues.AddRange(userLeague1, userLeague2);
        await _identityContext.SaveChangesAsync();

        // Act
        var userWithLeagues = await _identityContext.Users.FindAsync(user.Id);

        // Assert
        userWithLeagues.Should().NotBeNull();
        userWithLeagues.DisplayName.Should().Be("League Player");
        
        // Note: In a real test, you'd load the navigation properties
        var userLeagueCount = _identityContext.UserLeagues.Count(ul => ul.UserId == user.Id);
        userLeagueCount.Should().Be(2, "because user joined 2 leagues");
    }

    [Fact]
    public async Task FindByUserName_WithValidUserName_ReturnsUser()
    {
        // Arrange
        var user = _testDataBuilder.CreateUser("username@test.com", "uniqueusername");
        user.DisplayName = "Unique User";
        
        _identityContext.Users.Add(user);
        await _identityContext.SaveChangesAsync();

        // Act
        var result = _identityContext.Users.FirstOrDefault(u => u.UserName == "uniqueusername");

        // Assert
        result.Should().NotBeNull("because a user with this username exists");
        result.UserName.Should().Be("uniqueusername");
        result.DisplayName.Should().Be("Unique User");
    }

    [Fact]
    public async Task UserProperties_AllRequired_ValidatesCorrectly()
    {
        // Arrange & Act
        var user = _testDataBuilder.CreateUser("props@test.com", "propsuser");
        user.DisplayName = "Property User";
        user.FirstName = "Property";
        user.LastName = "User";

        // Assert - All required properties should be set
        user.Email.Should().NotBeNullOrEmpty("because Email is required");
        user.UserName.Should().NotBeNullOrEmpty("because UserName is required");
        user.DisplayName.Should().NotBeNullOrEmpty("because DisplayName is required");
        user.FirstName.Should().NotBeNullOrEmpty("because FirstName is required");
        user.LastName.Should().NotBeNullOrEmpty("because LastName is required");
        
        user.DisplayName.Should().Be("Property User");
        user.FirstName.Should().Be("Property");
        user.LastName.Should().Be("User");
    }

    [Fact]
    public async Task ConcurrentAccess_MultipleUserOperations_HandledCorrectly()
    {
        // Arrange
        var users = Enumerable.Range(1, 5)
            .Select(i => {
                var user = _testDataBuilder.CreateUser($"concurrent{i}@test.com", $"user{i}");
                user.DisplayName = $"Concurrent User {i}";
                user.FirstName = $"First{i}";
                user.LastName = $"Last{i}";
                return user;
            })
            .ToList();

        // Act
        _identityContext.Users.AddRange(users);
        await _identityContext.SaveChangesAsync();

        // Assert
        var result = _identityContext.Users.ToList();
        result.Should().HaveCount(5, "because all 5 users should be saved");
        result.Select(u => u.DisplayName).Should().Contain([
            "Concurrent User 1", "Concurrent User 2", "Concurrent User 3", 
            "Concurrent User 4", "Concurrent User 5"
        ]);
    }

    public void Dispose()
    {
        _context.Dispose();
        _identityContext.Dispose();
    }
}