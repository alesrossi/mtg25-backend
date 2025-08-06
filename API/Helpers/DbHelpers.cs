using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace API.Helpers;

public static class DbHelpers
{
    public static async Task EnsureDatabasesCreated(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
    
        var mainContext = scope.ServiceProvider.GetRequiredService<MainContext>();
        var identityContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
    
        await mainContext.Database.EnsureCreatedAsync();
        await identityContext.Database.EnsureCreatedAsync();
    }
}