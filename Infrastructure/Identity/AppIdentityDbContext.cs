using Core.Enums;
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
        public DbSet<LeagueRoleAssignment> LeagueRoleAssignments { get; set; }
        public DbSet<AppUserFriend> AppUserFriends { get; set; }
        public DbSet<Round> Rounds { get; set; }
        public DbSet<AppUserRound> UserRounds { get; set; }

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
                .HasConversion<string?>(
                    v => v.HasValue ? v.Value.ToCode() : null,
                    v => string.IsNullOrWhiteSpace(v) ? (Currency?)null : CurrencyExtensions.ParseOrDefault(v, Currency.Eur))
                .HasDefaultValue(Currency.Eur);

            builder.Entity<Settings>()
                .Property(s => s.LanguageUi)
                .HasConversion<string>(
                    v => v.ToCode(),
                    v => LanguageExtensions.ParseOrDefault(v, Language.It))
                .HasDefaultValue(Language.It);

            builder.Entity<Settings>()
                .Property(s => s.LanguageCards)
                .HasConversion<string>(
                    v => v.ToCode(),
                    v => LanguageExtensions.ParseOrDefault(v, Language.En))
                .HasDefaultValue(Language.En);

            builder.Entity<Settings>()
                .Property(s => s.EnabledLocation)
                .HasDefaultValue(false);

            builder.Entity<League>()
                .Property(l => l.Format)
                .HasConversion<string>(
                    v => v.ToCode(),
                    v => DeckFormatExtensions.ParseOrDefault(v, DeckFormat.Standard));

            builder.Entity<Notification>()
                .HasOne(ul => ul.AppUser)
                .WithMany(u => u.Notifications)
                .HasForeignKey(ul => ul.AppUserId);
            
            builder.Entity<Notification>()
                .Property(n => n.IsRead)
                .HasDefaultValue(false);
            
            builder.Entity<Notification>()
                .Property(n => n.Approval)
                .HasDefaultValue(false);
            
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

            builder.Entity<AppUserLeague>()
                .Property(ul => ul.AvgPosition)
                .HasColumnName("AvgScore");

            builder.Entity<LeagueRoleAssignment>()
                .HasIndex(lr => new { lr.LeagueId, lr.UserId })
                .IsUnique();

        builder.Entity<LeagueRoleAssignment>()
            .Property(lr => lr.Roles)
            .HasConversion<string>();

            builder.Entity<LeagueRoleAssignment>()
                .HasOne(lr => lr.User)
                .WithMany(u => u.LeagueRoles)
                .HasForeignKey(lr => lr.UserId);

            builder.Entity<LeagueRoleAssignment>()
                .HasOne(lr => lr.League)
                .WithMany(l => l.RoleAssignments)
                .HasForeignKey(lr => lr.LeagueId);

            builder.Entity<AppUserFriend>()
                .HasKey(f => new { f.UserId, f.FriendId });

            builder.Entity<AppUserFriend>()
                .HasOne(f => f.User)
                .WithMany(u => u.Friendships)
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AppUserFriend>()
                .HasOne(f => f.Friend)
                .WithMany(u => u.FriendshipsReceived)
                .HasForeignKey(f => f.FriendId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AppUserFriend>()
                .HasOne(f => f.RequestedBy)
                .WithMany()
                .HasForeignKey(f => f.RequestedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AppUserFriend>()
                .Property(f => f.Status)
                .HasConversion<string>()
                .HasDefaultValue(FriendshipStatus.Pending);

            builder.Entity<AppUserFriend>()
                .HasIndex(f => new { f.FriendId, f.UserId })
                .IsUnique();

            builder.Entity<Round>()
                .HasOne(r => r.League)
                .WithMany()
                .HasForeignKey(r => r.LeagueId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Round>()
                .Property(r => r.Status)
                .HasDefaultValue(Status.NotPlayed);

            builder.Entity<AppUserRound>()
                .HasKey(ur => new { ur.UserId, ur.RoundId });

            builder.Entity<AppUserRound>()
                .HasOne(ur => ur.User)
                .WithMany()
                .HasForeignKey(ur => ur.UserId);

            builder.Entity<AppUserRound>()
                .HasOne(ur => ur.Round)
                .WithMany(r => r.Players)
                .HasForeignKey(ur => ur.RoundId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
