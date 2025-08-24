using Core.Models.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity
{
    public class AppIdentityDbContext : IdentityDbContext<AppUser>
    {
        public AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options) : base(options)
        {
        }

        public DbSet<League> Leagues { get; set; }
        public DbSet<AppUserLeague> UserLeagues { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure UserLeague entity
            builder.Entity<AppUserLeague>()
                .HasKey(ul => new { ul.UserId, ul.LeagueId }); // Composite primary key

            builder.Entity<AppUserLeague>()
                .HasOne(ul => ul.User)
                .WithMany(u => u.UserLeagues)
                .HasForeignKey(ul => ul.UserId);

            builder.Entity<AppUserLeague>()
                .HasOne(ul => ul.League)
                .WithMany(l => l.UserLeagues)
                .HasForeignKey(ul => ul.LeagueId);
        }
    }
}