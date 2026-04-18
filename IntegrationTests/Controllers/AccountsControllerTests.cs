using System.Net;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using API.Dtos.Accounts;
using API.Services;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Moq;
using TestUtilities.Authentication;
using TestUtilities.Builders;
using TestUtilities.Serialization;

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
            responseContent, JsonContentHelper.DefaultOptions);

        userDto.Should().NotBeNull();
        userDto.Email.Should().Be(registerRequest.Email);
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
        const string password = "Password123!";
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
            responseContent, JsonContentHelper.DefaultOptions);

        authDto.Should().NotBeNull();
        authDto.UserId.Should().NotBeNullOrEmpty();
        authDto.Token.Should().NotBeNullOrEmpty("because login should return an authentication token");
    }

    [Fact]
    public async Task LogoutUser_WithValidAuthentication_ReturnsSuccessMessage()
    {
        // Arrange
        var email = $"logoutuser_{Guid.NewGuid().ToString("N")[..8]}@test.com";
        const string password = "Password123!";
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
            loginResponseContent, JsonContentHelper.DefaultOptions);

        authDto.Should().NotBeNull();
        authDto.Token.Should().NotBeNull();

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

    // ── Google Login ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GoogleLogin_WithInvalidToken_Returns400()
    {
        var mockGoogleAuth = new Mock<IGoogleAuthService>();
        mockGoogleAuth
            .Setup(s => s.AuthenticateAsync(It.IsAny<string>()))
            .ReturnsAsync(GoogleAuthResult.Fail("Invalid Google token."));

        using var client = _factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGoogleAuthService>();
                services.AddSingleton(mockGoogleAuth.Object);
            })).CreateClient();

        var body = new StringContent(
            JsonSerializer.Serialize(new GoogleLoginDto { IdToken = "bad-token" }),
            Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/accounts/google", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GoogleLogin_WithValidToken_ExistingGoogleUser_ReturnsAuthDto()
    {
        var user = await CreateTestUserAsync($"googleuser_{Guid.NewGuid():N}", "googleuser");

        var mockGoogleAuth = new Mock<IGoogleAuthService>();
        mockGoogleAuth
            .Setup(s => s.AuthenticateAsync("valid-token"))
            .ReturnsAsync(GoogleAuthResult.Ok(user));

        using var client = _factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGoogleAuthService>();
                services.AddSingleton(mockGoogleAuth.Object);
            })).CreateClient();

        var body = new StringContent(
            JsonSerializer.Serialize(new GoogleLoginDto { IdToken = "valid-token" }),
            Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/accounts/google", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authDto = JsonSerializer.Deserialize<AuthDto>(
            await response.Content.ReadAsStringAsync(), JsonContentHelper.DefaultOptions);

        authDto.Should().NotBeNull();
        authDto!.Token.Should().NotBeNullOrEmpty();
        authDto.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task GoogleLogin_WithValidToken_NewUser_ReturnsAuthDto()
    {
        var newUser = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = $"newgoogle_{Guid.NewGuid():N}@example.com",
            UserName = $"newgoogle_{Guid.NewGuid():N}",
            FirstName = "New",
            LastName = "User",
            DisplayName = "New",
            IsGoogleAccount = true
        };

        // The user must exist in the DB for JwtService to generate a token
        await CreateUserInDatabaseAsync(newUser);

        var mockGoogleAuth = new Mock<IGoogleAuthService>();
        mockGoogleAuth
            .Setup(s => s.AuthenticateAsync("new-user-token"))
            .ReturnsAsync(GoogleAuthResult.Ok(newUser));

        using var client = _factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGoogleAuthService>();
                services.AddSingleton(mockGoogleAuth.Object);
            })).CreateClient();

        var body = new StringContent(
            JsonSerializer.Serialize(new GoogleLoginDto { IdToken = "new-user-token" }),
            Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/accounts/google", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authDto = JsonSerializer.Deserialize<AuthDto>(
            await response.Content.ReadAsStringAsync(), JsonContentHelper.DefaultOptions);

        authDto.Should().NotBeNull();
        authDto!.Token.Should().NotBeNullOrEmpty();
        authDto.UserId.Should().Be(newUser.Id);
        authDto.FirstName.Should().Be("New");
    }

    [Fact]
    public async Task GoogleLogin_WithMissingIdToken_Returns400()
    {
        using var client = _factory.CreateClient();

        var body = new StringContent(
            JsonSerializer.Serialize(new { IncludeRefreshToken = false }),
            Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/accounts/google", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PasswordLogin_WithGoogleAccount_Returns400()
    {
        var email = $"googleonly_{Guid.NewGuid():N}@test.com";
        await CreateGoogleUserInDatabaseAsync(email);
        using var client = _factory.CreateClient();

        var body = new StringContent(
            JsonSerializer.Serialize(new LoginDto { Email = email, Password = "Password123!" }),
            Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/accounts/login", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "because Google accounts must use the /google endpoint, not password login");
    }

    // ── Avatar ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSettings_WithAvatar_PersistsAvatarUrl()
    {
        var email = $"avatar_{Guid.NewGuid():N}@test.com";
        var user = await CreateTestUserAsync(email, "avataruser");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        const string avatarUrl = "https://cards.scryfall.io/art_crop/front/a/b/abc123.jpg";
        var updateDto = new UpdateSettingsDto { Avatar = avatarUrl };
        var content = new StringContent(JsonSerializer.Serialize(updateDto), Encoding.UTF8, "application/json");

        var response = await client.PutAsync("/api/accounts/settings", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = JsonSerializer.Deserialize<SettingsForUserDto>(
            await response.Content.ReadAsStringAsync(), JsonContentHelper.DefaultOptions);
        dto!.AppUser.Avatar.Should().Be(avatarUrl);
    }

    [Fact]
    public async Task UpdateSettings_AvatarThenGet_ReturnsPersistedAvatar()
    {
        var email = $"avatar_get_{Guid.NewGuid():N}@test.com";
        var user = await CreateTestUserAsync(email, "avatargetuser");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        const string avatarUrl = "https://cards.scryfall.io/art_crop/front/c/d/def456.jpg";
        var updateDto = new UpdateSettingsDto { Avatar = avatarUrl };
        var content = new StringContent(JsonSerializer.Serialize(updateDto), Encoding.UTF8, "application/json");
        await client.PutAsync("/api/accounts/settings", content);

        var getResponse = await client.GetAsync("/api/accounts/settings");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = JsonSerializer.Deserialize<SettingsForUserDto>(
            await getResponse.Content.ReadAsStringAsync(), JsonContentHelper.DefaultOptions);
        dto!.AppUser.Avatar.Should().Be(avatarUrl);
    }

    [Fact]
    public async Task UpdateSettings_AvatarDoesNotAffectOtherFields()
    {
        var email = $"avatar_isolation_{Guid.NewGuid():N}@test.com";
        var user = await CreateTestUserAsync(email, "avatarisolationuser");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var setupDto = new UpdateSettingsDto { CompanionName = "Skullclamp" };
        var setupContent = new StringContent(JsonSerializer.Serialize(setupDto), Encoding.UTF8, "application/json");
        await client.PutAsync("/api/accounts/settings", setupContent);

        var avatarDto = new UpdateSettingsDto { Avatar = "https://cards.scryfall.io/art_crop/front/e/f/ef789.jpg" };
        var avatarContent = new StringContent(JsonSerializer.Serialize(avatarDto), Encoding.UTF8, "application/json");
        var response = await client.PutAsync("/api/accounts/settings", avatarContent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = JsonSerializer.Deserialize<SettingsForUserDto>(
            await response.Content.ReadAsStringAsync(), JsonContentHelper.DefaultOptions);
        dto!.AppUser.CompanionName.Should().Be("Skullclamp");
        dto.AppUser.Avatar.Should().Be("https://cards.scryfall.io/art_crop/front/e/f/ef789.jpg");
    }

    [Fact]
    public async Task UpdateSettings_WithoutAvatar_LeavesAvatarNull()
    {
        var email = $"avatar_null_{Guid.NewGuid():N}@test.com";
        var user = await CreateTestUserAsync(email, "avatarnulluser");
        using var client = _factory.CreateClientWithUser(user.Id, user.UserName!, user.Email!);

        var updateDto = new UpdateSettingsDto { CompanionName = "Jace" };
        var content = new StringContent(JsonSerializer.Serialize(updateDto), Encoding.UTF8, "application/json");
        var response = await client.PutAsync("/api/accounts/settings", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = JsonSerializer.Deserialize<SettingsForUserDto>(
            await response.Content.ReadAsStringAsync(), JsonContentHelper.DefaultOptions);
        dto!.AppUser.Avatar.Should().BeNull();
    }

    #region Helper Methods
    private Task<AppUser> CreateTestUserAsync(string baseEmail, string baseUserName) =>
        TestUserFactory.CreateAsync(_factory.Services, _testDataBuilder, baseEmail, baseUserName);

    private Task<AppUser> CreateTestUserWithPasswordAsync(string email, string userName, string password) =>
        TestUserFactory.CreateAsync(_factory.Services, _testDataBuilder, email, userName, requirePassword: true, password: password);

    private async Task CreateUserInDatabaseAsync(AppUser user)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    private async Task CreateGoogleUserInDatabaseAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser
        {
            Email = email,
            UserName = email,
            FirstName = "Google",
            LastName = "User",
            DisplayName = "Google",
            IsGoogleAccount = true
        };
        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(e => e.Description)));
    }

    private async Task VerifyUserExistsInDatabase(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = await userManager.FindByEmailAsync(email);
        user.Should().NotBeNull($"because user with email {email} should exist in database");
    }

    #endregion
}