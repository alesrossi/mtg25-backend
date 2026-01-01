using System.Net;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using API.Dtos.Accounts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;
using TestUtilities.Authentication;
using TestUtilities.Builders;

namespace IntegrationTests.Controllers;

/// <summary>
/// Integration tests for Account endpoints.
/// Tests user registration, login, logout, and email verification functionality.
/// </summary>
[Collection("Integration Tests")]
public class AccountsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly TestDataBuilder _testDataBuilder;

    public AccountsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _testDataBuilder = new TestDataBuilder();
    }

    [Fact]
    public async Task CheckEmailExists_WithExistingEmail_ReturnsTrue()
    {
        // Arrange
        var existingEmail = $"existing_{Guid.NewGuid().ToString("N")[..8]}@test.com";
        var user = await CreateTestUserAsync(existingEmail, "existinguser_CheckEmailExists_WithExistingEmail_ReturnsTrue");
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/accounts/emailexists/{Uri.EscapeDataString(user.Email!)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "because email existence check should always work");

        var responseContent = await response.Content.ReadAsStringAsync();
        var emailExists = JsonSerializer.Deserialize<bool>(responseContent);

        emailExists.Should().BeTrue("because the email already exists in the system");
    }

    [Fact]
    public async Task CheckEmailExists_WithNonExistentEmail_ReturnsFalse()
    {
        // Arrange
        var nonExistentEmail = $"nonexistent_{Guid.NewGuid().ToString("N")[..8]}@test.com";
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/accounts/emailexists/{Uri.EscapeDataString(nonExistentEmail)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseContent = await response.Content.ReadAsStringAsync();
        var emailExists = JsonSerializer.Deserialize<bool>(responseContent);

        emailExists.Should().BeFalse("because the email doesn't exist in the system");
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@test.com")]
    [InlineData("user@")]
    public async Task CheckEmailExists_WithInvalidEmailFormat_HandlesGracefully(string invalidEmail)
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/accounts/emailexists/{Uri.EscapeDataString(invalidEmail)}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterUser_WithValidData_CreatesUser()
    {
        // Arrange
        using var client = _factory.CreateClient();
        
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var registerRequest = new RegisterDto
        {
            DisplayName = $"Test User {uniqueId}",
            FirstName = "Test",
            LastName = "User",
            Email = $"newuser_{uniqueId}@test.com",
            Password = "Password123!"
        };

        var json = JsonSerializer.Serialize(registerRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/accounts/register", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "because valid registration data should create a new user");

        var responseContent = await response.Content.ReadAsStringAsync();
        var userDto = JsonSerializer.Deserialize<UserDto>(
            responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        userDto.Should().NotBeNull();
        userDto!.Email.Should().Be(registerRequest.Email);
        userDto.DisplayName.Should().Be(registerRequest.DisplayName);

        // Verify user was actually created in database
        await VerifyUserExistsInDatabase(registerRequest.Email);
    }

    [Fact]
    public async Task RegisterUser_WithExistingEmail_ReturnsBadRequest()
    {
        // Arrange
        var existingEmail = $"duplicate_{Guid.NewGuid().ToString("N")[..8]}@test.com";
        var user = await CreateTestUserAsync(existingEmail, "existinguser");
        using var client = _factory.CreateClient();

        var registerRequest = new RegisterDto
        {
            DisplayName = "Duplicate User",
            FirstName = "Duplicate",
            LastName = "User",
            Email = user.Email!,
            Password = "Password123!"
        };

        var json = JsonSerializer.Serialize(registerRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/accounts/register", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "because duplicate emails should be rejected");
    }

    [Theory]
    [InlineData("", "First", "Last", "email@test.com", "Password123!")] // Empty display name
    [InlineData("Display", "", "Last", "email@test.com", "Password123!")] // Empty first name
    [InlineData("Display", "First", "", "email@test.com", "Password123!")] // Empty last name
    [InlineData("Display", "First", "Last", "", "Password123!")] // Empty email
    [InlineData("Display", "First", "Last", "invalid-email", "Password123!")] // Invalid email
    [InlineData("Display", "First", "Last", "email@test.com", "")] // Empty password
    [InlineData("Display", "First", "Last", "email@test.com", "weak")] // Weak password
    public async Task RegisterUser_WithInvalidData_ReturnsBadRequest(
        string displayName, string firstName, string lastName, string email, string password)
    {
        // Arrange
        using var client = _factory.CreateClient();
        
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var registerRequest = new RegisterDto
        {
            DisplayName = displayName,
            FirstName = firstName,
            LastName = lastName,
            Email = string.IsNullOrEmpty(email) ? email : $"{uniqueId}_{email}",
            Password = password
        };

        var json = JsonSerializer.Serialize(registerRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/accounts/register", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "because invalid registration data should be rejected");
    }

    [Fact]
    public async Task LoginUser_WithValidCredentials_ReturnsAuthToken()
    {
        // Arrange
        var email = $"loginuser_{Guid.NewGuid().ToString("N")[..8]}@test.com";
        var password = "Password123!";
        var user = await CreateTestUserWithPasswordAsync(email, "loginuser", password);
        using var client = _factory.CreateClient();

        var loginRequest = new LoginDto
        {
            Email = user.Email!,
            Password = password
        };

        var json = JsonSerializer.Serialize(loginRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/accounts/login", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "because valid credentials should allow login");

        var responseContent = await response.Content.ReadAsStringAsync();
        var authDto = JsonSerializer.Deserialize<AuthDto>(
            responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        authDto.Should().NotBeNull();
        authDto!.UserId.Should().NotBeNullOrEmpty();
        authDto.Token.Should().NotBeNullOrEmpty("because login should return an authentication token");
    }

    [Fact]
    public async Task LogoutUser_WithValidAuthentication_ReturnsSuccessMessage()
    {
        // Arrange
        var email = $"logoutuser_{Guid.NewGuid().ToString("N")[..8]}@test.com";
        var password = "Password123!";
        var user = await CreateTestUserWithPasswordAsync(email, "logoutuser", password);
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var loginRequest = new LoginDto
        {
            Email = user.Email!,
            Password = password
        };

        var loginJson = JsonSerializer.Serialize(loginRequest);
        var loginContent = new StringContent(loginJson, Encoding.UTF8, "application/json");
        var loginResponse = await client.PostAsync("/api/accounts/login", loginContent);

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "because valid credentials should allow login before logout");

        var loginResponseContent = await loginResponse.Content.ReadAsStringAsync();
        var authDto = JsonSerializer.Deserialize<AuthDto>(
            loginResponseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        authDto.Should().NotBeNull();
        authDto!.Token.Should().NotBeNull();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authDto.Token);

        // Act
        var response = await client.GetAsync("/api/accounts/logout");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "because authenticated users should be able to logout");

        var responseContent = await response.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<JsonElement>(responseContent);

        payload.TryGetProperty("message", out var messageProperty).Should().BeTrue(
            "because logout should return a confirmation message");
        messageProperty.GetString().Should().Be("Logged out successfully");
    }

    [Fact]
    public async Task LoginUser_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var email = $"badlogin_{Guid.NewGuid().ToString("N")[..8]}@test.com";
        await CreateTestUserWithPasswordAsync(email, "badlogin", "CorrectPassword123!");
        using var client = _factory.CreateClient();

        var loginRequest = new LoginDto
        {
            Email = email,
            Password = "WrongPassword123!"
        };

        var json = JsonSerializer.Serialize(loginRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/accounts/login", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "because invalid credentials should be rejected");
    }

    [Fact]
    public async Task LoginUser_WithNonExistentEmail_ReturnsUnauthorized()
    {
        // Arrange
        using var client = _factory.CreateClient();

        var loginRequest = new LoginDto
        {
            Email = $"nonexistent_{Guid.NewGuid().ToString("N")[..8]}@test.com",
            Password = "Password123!"
        };

        var json = JsonSerializer.Serialize(loginRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/accounts/login", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "because non-existent users should be rejected");
    }
    
    [Fact]
    public async Task LogoutUser_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var client = _factory.CreateClient(); // No authentication

        // Act
        var response = await client.GetAsync("/api/accounts/logout");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "because unauthenticated users cannot logout");
    }

    // Commented out tests that may fail due to missing implementation details
    [Fact]
    public async Task RegisterUser_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        using var client = _factory.CreateClient();
        
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var registerRequest = new RegisterDto
        {
            DisplayName = $"José María Ñoño {uniqueId}",
            FirstName = "José María",
            LastName = "Ñoño",
            Email = $"jose.maria.nono_{uniqueId}@test.com",
            Password = "Password123!"
        };

        var json = JsonSerializer.Serialize(registerRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/accounts/register", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "because names with special characters should be supported");
    }

    #region Helper Methods
    private Task<AppUser> CreateTestUserAsync(string baseEmail, string baseUserName) =>
        TestUserFactory.CreateAsync(_factory.Services, _testDataBuilder, baseEmail, baseUserName);

    private Task<AppUser> CreateTestUserWithPasswordAsync(string email, string userName, string password) =>
        TestUserFactory.CreateAsync(_factory.Services, _testDataBuilder, email, userName, requirePassword: true, password: password);

    private async Task VerifyUserExistsInDatabase(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        
        var user = await userManager.FindByEmailAsync(email);
        user.Should().NotBeNull($"because user with email {email} should exist in database");
    }

    #endregion
}
