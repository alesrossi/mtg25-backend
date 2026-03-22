using API.Configuration;
using API.Services;
using Core.Models.Identity;
using FluentAssertions;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace UnitTests.Services;

public class GoogleAuthServiceTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<IGoogleTokenValidator> _tokenValidatorMock;
    private readonly IOptions<GoogleAuthConfig> _config;
    private const string TestClientId = "test-client-id.apps.googleusercontent.com";

    public GoogleAuthServiceTests()
    {
        var store = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            store.Object, null, null, null, null, null, null, null, null);
        _tokenValidatorMock = new Mock<IGoogleTokenValidator>();
        _config = Options.Create(new GoogleAuthConfig { ClientId = TestClientId });
    }

    private GoogleAuthService CreateService() =>
        new(_userManagerMock.Object, _tokenValidatorMock.Object, _config, NullLogger<GoogleAuthService>.Instance);

    private static GoogleJsonWebSignature.Payload MakePayload(
        string email = "user@example.com",
        string? givenName = "John",
        string? familyName = "Doe") =>
        new() { Email = email, GivenName = givenName, FamilyName = familyName };

    // ── Token validation ────────────────────────────────────────────────────

    [Fact]
    public async Task AuthenticateAsync_InvalidToken_ReturnsFail()
    {
        _tokenValidatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), TestClientId))
            .ThrowsAsync(new InvalidJwtException("bad token"));

        var result = await CreateService().AuthenticateAsync("bad-token");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Invalid Google token.");
    }

    // ── Existing user ────────────────────────────────────────────────────────

    [Fact]
    public async Task AuthenticateAsync_ExistingGoogleAccount_ReturnsOk()
    {
        var existingUser = new AppUser
        {
            Id = "uid-1", Email = "user@example.com",
            DisplayName = "User", FirstName = "User", LastName = "One",
            IsGoogleAccount = true
        };
        _tokenValidatorMock
            .Setup(v => v.ValidateAsync("valid-token", TestClientId))
            .ReturnsAsync(MakePayload("user@example.com"));
        _userManagerMock
            .Setup(m => m.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(existingUser);

        var result = await CreateService().AuthenticateAsync("valid-token");

        result.Succeeded.Should().BeTrue();
        result.User.Should().Be(existingUser);
        _userManagerMock.Verify(m => m.CreateAsync(It.IsAny<AppUser>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_ExistingPasswordAccount_ReturnsFail()
    {
        var passwordUser = new AppUser
        {
            Id = "uid-2", Email = "user@example.com",
            DisplayName = "User", FirstName = "User", LastName = "Two",
            IsGoogleAccount = false
        };
        _tokenValidatorMock
            .Setup(v => v.ValidateAsync("valid-token", TestClientId))
            .ReturnsAsync(MakePayload("user@example.com"));
        _userManagerMock
            .Setup(m => m.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(passwordUser);

        var result = await CreateService().AuthenticateAsync("valid-token");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("already exists");
        _userManagerMock.Verify(m => m.CreateAsync(It.IsAny<AppUser>()), Times.Never);
    }

    // ── New user registration ─────────────────────────────────────────────────

    [Fact]
    public async Task AuthenticateAsync_NewUser_CreatesUserAndReturnsOk()
    {
        _tokenValidatorMock
            .Setup(v => v.ValidateAsync("valid-token", TestClientId))
            .ReturnsAsync(MakePayload("new@example.com", "Alice", "Smith"));
        _userManagerMock
            .Setup(m => m.FindByEmailAsync("new@example.com"))
            .ReturnsAsync((AppUser?)null);
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<AppUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await CreateService().AuthenticateAsync("valid-token");

        result.Succeeded.Should().BeTrue();
        result.User.Should().NotBeNull();
        result.User!.Email.Should().Be("new@example.com");
        result.User.FirstName.Should().Be("Alice");
        result.User.LastName.Should().Be("Smith");
    }

    [Fact]
    public async Task AuthenticateAsync_NewUser_SetsIsGoogleAccountTrue()
    {
        AppUser? captured = null;
        _tokenValidatorMock
            .Setup(v => v.ValidateAsync("valid-token", TestClientId))
            .ReturnsAsync(MakePayload("new@example.com"));
        _userManagerMock
            .Setup(m => m.FindByEmailAsync("new@example.com"))
            .ReturnsAsync((AppUser?)null);
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<AppUser>()))
            .Callback<AppUser>(u => captured = u)
            .ReturnsAsync(IdentityResult.Success);

        await CreateService().AuthenticateAsync("valid-token");

        captured.Should().NotBeNull();
        captured!.IsGoogleAccount.Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticateAsync_NewUser_WithFirstName_UsesFirstNameAsDisplayName()
    {
        AppUser? captured = null;
        _tokenValidatorMock
            .Setup(v => v.ValidateAsync("valid-token", TestClientId))
            .ReturnsAsync(MakePayload("new@example.com", givenName: "Bob"));
        _userManagerMock
            .Setup(m => m.FindByEmailAsync("new@example.com"))
            .ReturnsAsync((AppUser?)null);
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<AppUser>()))
            .Callback<AppUser>(u => captured = u)
            .ReturnsAsync(IdentityResult.Success);

        await CreateService().AuthenticateAsync("valid-token");

        captured!.DisplayName.Should().Be("Bob");
    }

    [Fact]
    public async Task AuthenticateAsync_NewUser_WithNoFirstName_UsesEmailPrefixAsDisplayName()
    {
        AppUser? captured = null;
        _tokenValidatorMock
            .Setup(v => v.ValidateAsync("valid-token", TestClientId))
            .ReturnsAsync(MakePayload("bob@example.com", givenName: null));
        _userManagerMock
            .Setup(m => m.FindByEmailAsync("bob@example.com"))
            .ReturnsAsync((AppUser?)null);
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<AppUser>()))
            .Callback<AppUser>(u => captured = u)
            .ReturnsAsync(IdentityResult.Success);

        await CreateService().AuthenticateAsync("valid-token");

        captured!.DisplayName.Should().Be("bob");
    }

    [Fact]
    public async Task AuthenticateAsync_NewUser_CreationFails_ReturnsFail()
    {
        _tokenValidatorMock
            .Setup(v => v.ValidateAsync("valid-token", TestClientId))
            .ReturnsAsync(MakePayload("new@example.com"));
        _userManagerMock
            .Setup(m => m.FindByEmailAsync("new@example.com"))
            .ReturnsAsync((AppUser?)null);
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<AppUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "DB error" }));

        var result = await CreateService().AuthenticateAsync("valid-token");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Failed to create account.");
    }
}
