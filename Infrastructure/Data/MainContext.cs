using Core.Enums;
using Core.Models;
using Core.Models.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class MainContext : IdentityDbContext<AppUser>
{
    public MainContext(DbContextOptions<MainContext> options) : base(options)
    {
    }

    public DbSet<Card> Cards { get; set; }
    public DbSet<Collection> Collections { get; set; }
    public DbSet<Deck> Decks { get; set; }
    public DbSet<DeckCard> DeckCards { get; set; }
    public DbSet<DeckCommit> DeckCommits { get; set; }
    public DbSet<DeckCommitParent> DeckCommitParents { get; set; }
    public DbSet<DeckTree> DeckTrees { get; set; }
    public DbSet<DeckTreeEntry> DeckTreeEntries { get; set; }
    public DbSet<DeckBranch> DeckBranches { get; set; }
    public DbSet<Wishlist> Wishlists { get; set; }
    public DbSet<WishlistCard> WishlistCards { get; set; }
    public DbSet<TradeBinder> TradeBinders { get; set; }
    public DbSet<BinderCard> BinderCards { get; set; }

    public DbSet<League> Leagues { get; set; }
    public DbSet<AppUserLeague> UserLeagues { get; set; }
    public DbSet<Settings> Settings { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<LeagueRoleAssignment> LeagueRoleAssignments { get; set; }
    public DbSet<AppUserFriend> AppUserFriends { get; set; }
    public DbSet<Round> Rounds { get; set; }
    public DbSet<AppUserRound> UserRounds { get; set; }
    public DbSet<RoundParticipant> RoundParticipants { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Deck>(entity =>
        {
            entity.HasOne(d => d.CurrentBranch)
                .WithMany()
                .HasForeignKey(d => d.CurrentBranchId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.CurrentCommit)
                .WithMany()
                .HasForeignKey(d => d.CurrentCommitId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(d => d.DeckList)
                .HasDefaultValue(string.Empty);
        });

        modelBuilder.Entity<Collection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Wishlist>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Description)
                .HasMaxLength(2000);

            entity.Property(e => e.OwnerId)
                .IsRequired();

            entity.HasMany(e => e.WishlistCards)
                .WithOne(c => c.Wishlist)
                .HasForeignKey(c => c.WishlistId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WishlistCard>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.ScryfallId).IsRequired();
            entity.Property(e => e.Name).IsRequired();

            entity.Property(e => e.DesiredQuantity)
                .HasDefaultValue(1);

            entity.Property(e => e.Language)
                .HasConversion(
                    v => v.HasValue ? v.Value.ToCode() : null,
                    v => CardLanguageExtensions.ParseNullable(v));
        });

        modelBuilder.Entity<Card>(entity =>
        {
            entity.Property(e => e.Language)
                .HasConversion(
                    v => v.ToCode(),
                    v => CardLanguageExtensions.ParseOrDefault(v, CardLanguage.En));

            entity.Property(e => e.PurchasePriceCurrency)
                .HasConversion(
                    v => v.ToCode(),
                    v => CurrencyExtensions.ParseOrDefault(v, Currency.Usd));
        });

        modelBuilder.Entity<Deck>(entity =>
        {
            entity.Property(e => e.TotalPriceCurrency)
                .HasConversion(
                    v => v.HasValue ? v.Value.ToCode() : null,
                    v => CurrencyExtensions.ParseNullable(v));
        });

        modelBuilder.Entity<Wishlist>(entity =>
        {
            entity.Property(e => e.TotalPriceCurrency)
                .HasConversion(
                    v => v.HasValue ? v.Value.ToCode() : null,
                    v => CurrencyExtensions.ParseNullable(v));
        });

        modelBuilder.Entity<DeckCard>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();
            
            // Configure relationship with Deck
            entity.HasOne(dc => dc.Deck)
                .WithMany(d => d.DeckCards)
                .HasForeignKey(dc => dc.DeckId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Configure optional relationship with owned Card
            entity.HasOne(dc => dc.OwnedCard)
                .WithMany()
                .HasForeignKey(dc => dc.OwnedCardId)
                .OnDelete(DeleteBehavior.SetNull);
                
            // Ensure required properties are not null
            entity.Property(dc => dc.ScryfallId).IsRequired();
            entity.Property(dc => dc.Name).IsRequired();
            entity.Property(dc => dc.SetCode).IsRequired();
            entity.Property(dc => dc.TypeLine).IsRequired();

            entity.HasIndex(dc => dc.DeckId);
            entity.HasIndex(dc => new { dc.DeckId, dc.Name });
        });

        modelBuilder.Entity<Deck>(entity =>
        {
            entity.Property(e => e.Format)
                .HasConversion(
                    v => v.ToCode(),
                    v => DeckFormatExtensions.ParseOrDefault(v, DeckFormat.Standard));
        });

        modelBuilder.Entity<DeckCommit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.AuthorId).IsRequired();
            entity.Property(e => e.Message).IsRequired();

            entity.HasOne(e => e.Deck)
                .WithMany(d => d.Commits)
                .HasForeignKey(e => e.DeckId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Tree)
                .WithMany()
                .HasForeignKey(e => e.TreeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.DeckId);
        });

        modelBuilder.Entity<DeckCommitParent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.HasOne(e => e.Commit)
                .WithMany(c => c.ParentLinks)
                .HasForeignKey(e => e.CommitId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ParentCommit)
                .WithMany(c => c.ChildLinks)
                .HasForeignKey(e => e.ParentCommitId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.CommitId, e.ParentCommitId })
                .IsUnique();
        });

        modelBuilder.Entity<DeckTree>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<DeckTreeEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.ScryfallId).IsRequired();

            entity.HasOne(e => e.Tree)
                .WithMany(t => t.Entries)
                .HasForeignKey(e => e.TreeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.TreeId, e.ScryfallId })
                .IsUnique();
        });

        modelBuilder.Entity<DeckBranch>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.CreatedByUserId).IsRequired();

            entity.HasOne(e => e.Deck)
                .WithMany(d => d.Branches)
                .HasForeignKey(e => e.DeckId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.HeadCommit)
                .WithMany()
                .HasForeignKey(e => e.HeadCommitId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.DeckId, e.Name })
                .IsUnique();
        });

        modelBuilder.Entity<TradeBinder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.OwnerId)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasMaxLength(2000);

            entity.HasMany(e => e.BinderCards)
                .WithOne(bc => bc.TradeBinder)
                .HasForeignKey(bc => bc.TradeBinderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BinderCard>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.QuantityToTrade)
                .HasDefaultValue(0);

            entity.Property(e => e.CardId)
                .IsRequired();

            entity.HasOne(e => e.Card)
                .WithMany()
                .HasForeignKey(e => e.CardId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(e => e.Card)
                .AutoInclude();
        });

        // Identity domain entities (migrated from AppIdentityDbContext)
        modelBuilder.Entity<Settings>()
            .HasOne(s => s.AppUser)
            .WithOne(u => u.Settings)
            .HasForeignKey<Settings>(s => s.AppUserId);

        modelBuilder.Entity<Settings>()
            .Property(s => s.MarketProvider)
            .HasConversion<string>()
            .HasDefaultValue(MarketProvider.Mkm);

        modelBuilder.Entity<Settings>()
            .Property(s => s.ReferencePrice)
            .HasConversion<string>()
            .HasDefaultValue(ReferencePrice.Avg);

        modelBuilder.Entity<Settings>()
            .Property(s => s.Currency)
            .HasConversion<string?>(
                v => v.HasValue ? v.Value.ToCode() : null,
                v => string.IsNullOrWhiteSpace(v) ? null : CurrencyExtensions.ParseOrDefault(v, Currency.Eur))
            .HasDefaultValue(Currency.Eur);

        modelBuilder.Entity<Settings>()
            .Property(s => s.LanguageUi)
            .HasConversion<string>(
                v => v.ToCode(),
                v => LanguageExtensions.ParseOrDefault(v, Language.It));

        modelBuilder.Entity<Settings>()
            .Property(s => s.LanguageCards)
            .HasConversion<string>(
                v => v.ToCode(),
                v => LanguageExtensions.ParseOrDefault(v, Language.En))
            .HasDefaultValue(Language.En);

        modelBuilder.Entity<Settings>()
            .Property(s => s.EnabledLocation)
            .HasDefaultValue(false);

        modelBuilder.Entity<League>()
            .Property(l => l.Format)
            .HasConversion<string>(
                v => v.ToCode(),
                v => DeckFormatExtensions.ParseOrDefault(v, DeckFormat.Standard));

        modelBuilder.Entity<Notification>()
            .HasOne(ul => ul.AppUser)
            .WithMany(u => u.Notifications)
            .HasForeignKey(ul => ul.AppUserId);

        modelBuilder.Entity<Notification>()
            .Property(n => n.IsRead)
            .HasDefaultValue(false);

        modelBuilder.Entity<Notification>()
            .Property(n => n.Approval)
            .HasDefaultValue(false);

        modelBuilder.Entity<AppUserLeague>()
            .HasKey(ul => new { ul.UserId, ul.LeagueId });

        modelBuilder.Entity<AppUserLeague>()
            .HasOne(ul => ul.User)
            .WithMany(u => u.UserLeagues)
            .HasForeignKey(ul => ul.UserId);

        modelBuilder.Entity<AppUserLeague>()
            .HasOne(ul => ul.League)
            .WithMany(l => l.UserLeagues)
            .HasForeignKey(ul => ul.LeagueId);

        modelBuilder.Entity<AppUserLeague>()
            .Property(ul => ul.AvgPosition)
            .HasColumnName("AvgScore");

        modelBuilder.Entity<LeagueRoleAssignment>()
            .HasIndex(lr => new { lr.LeagueId, lr.UserId })
            .IsUnique();

        modelBuilder.Entity<LeagueRoleAssignment>()
            .Property(lr => lr.Roles)
            .HasConversion<string>();

        modelBuilder.Entity<LeagueRoleAssignment>()
            .HasOne(lr => lr.User)
            .WithMany(u => u.LeagueRoles)
            .HasForeignKey(lr => lr.UserId);

        modelBuilder.Entity<LeagueRoleAssignment>()
            .HasOne(lr => lr.League)
            .WithMany(l => l.RoleAssignments)
            .HasForeignKey(lr => lr.LeagueId);

        modelBuilder.Entity<AppUserFriend>()
            .HasKey(f => new { f.UserId, f.FriendId });

        modelBuilder.Entity<AppUserFriend>()
            .HasOne(f => f.User)
            .WithMany(u => u.Friendships)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AppUserFriend>()
            .HasOne(f => f.Friend)
            .WithMany(u => u.FriendshipsReceived)
            .HasForeignKey(f => f.FriendId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AppUserFriend>()
            .HasOne(f => f.RequestedBy)
            .WithMany()
            .HasForeignKey(f => f.RequestedById)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AppUserFriend>()
            .Property(f => f.Status)
            .HasConversion<string>()
            .HasDefaultValue(FriendshipStatus.Pending);

        modelBuilder.Entity<AppUserFriend>()
            .HasIndex(f => new { f.FriendId, f.UserId })
            .IsUnique();

        modelBuilder.Entity<Round>()
            .HasOne(r => r.League)
            .WithMany()
            .HasForeignKey(r => r.LeagueId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Round>()
            .Property(r => r.Status)
            .HasDefaultValue(Status.NotPlayed);

        modelBuilder.Entity<AppUserRound>()
            .HasKey(ur => new { ur.UserId, ur.RoundId });

        modelBuilder.Entity<AppUserRound>()
            .HasOne(ur => ur.User)
            .WithMany()
            .HasForeignKey(ur => ur.UserId);

        modelBuilder.Entity<AppUserRound>()
            .HasOne(ur => ur.Round)
            .WithMany(r => r.Players)
            .HasForeignKey(ur => ur.RoundId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RoundParticipant>()
            .HasKey(rp => new { rp.UserId, rp.RoundId });

        modelBuilder.Entity<RoundParticipant>()
            .HasOne(rp => rp.User)
            .WithMany()
            .HasForeignKey(rp => rp.UserId);

        modelBuilder.Entity<RoundParticipant>()
            .HasOne(rp => rp.Round)
            .WithMany(r => r.Participants)
            .HasForeignKey(rp => rp.RoundId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
