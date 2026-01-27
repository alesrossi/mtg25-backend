using API.Dtos.Accounts;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;

namespace API.Endpoints.Accounts;

public static partial class AccountsEndpoints
{
    private static async Task<bool> CheckEmailExistsAsyncHelper(
        UserManager<AppUser> userManager,
        string email)
    {
        return await userManager.FindByEmailAsync(email) != null;
    }

    private static SettingsForUserDto MapToDto(Settings settings, AppUser user)
    {
        return new SettingsForUserDto
        {
            Id = settings.Id,
            MarketProvider = settings.MarketProvider,
            ReferencePrice = settings.ReferencePrice,
            Currency = settings.Currency,
            LanguageUi = settings.LanguageUi,
            LanguageCards = settings.LanguageCards,
            EnabledLocation = settings.EnabledLocation,
            AppUserId = user.Id,
            AppUser = MapToDto(user)
        };
    }
    
    private static UserDto MapToDto(AppUser user)
    {
        return new UserDto
        {
            Email = user.Email,
            DisplayName = user.DisplayName,
            FirstName = user.FirstName,
            LastName = user.LastName
        };
    }
}