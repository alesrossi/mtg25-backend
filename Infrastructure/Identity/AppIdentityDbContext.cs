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
        public DbSet<Settings> Settings { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<Settings>()
                .HasOne(s => s.AppUser)
                .WithOne(u => u.Settings)
                .HasForeignKey<Settings>(s => s.AppUserId);

            builder.Entity<Settings>()
                .Property(s => s.MarketProvider)
                .HasConversion<string>()
                .HasDefaultValue(MarketProvider.Mkm);

            builder.Entity<Settings>()
                .Property(s => s.ReferencePrice)
                .HasConversion<string>()
                .HasDefaultValue(ReferencePrice.Avg);

            builder.Entity<Settings>()
                .Property(s => s.Currency)
                .HasConversion<string>()
                .HasDefaultValue(Currency.Eur);

            builder.Entity<Settings>()
                .Property(s => s.LanguageUi)
                .HasDefaultValue("It");

            builder.Entity<Settings>()
                .Property(s => s.LanguageCards)
                .HasDefaultValue("En");

            builder.Entity<Settings>()
                .Property(s => s.EnabledLocation)
                .HasDefaultValue(false);

            builder.Entity<Notification>()
                .HasOne(ul => ul.AppUser)
                .WithMany(u => u.Notifications)
                .HasForeignKey(ul => ul.AppUserId);
            
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
