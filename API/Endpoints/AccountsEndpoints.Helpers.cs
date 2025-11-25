using Core.Models.Identity;
using Microsoft.AspNetCore.Identity;

namespace API.Endpoints;

public static partial class AccountsEndpoints
{
    private static async Task<bool> CheckEmailExistsAsyncHelper(
        UserManager<AppUser> userManager,
        string email)
    {
        return await userManager.FindByEmailAsync(email) != null;
    }
}
