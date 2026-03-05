using Core.Models.Identity;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;

namespace API.Extensions
{
    public static class IdentityServiceExtensions
    {
        public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration config)
        {
            var builder = services.AddIdentityCore<AppUser>();

            builder = new IdentityBuilder(builder.UserType, builder.Services);
            builder.AddEntityFrameworkStores<MainContext>();
            builder.AddSignInManager<SignInManager<AppUser>>();

            return services;
        }
    }
}
