using System.Reflection;
using Core.Enums;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class MainContext : DbContext
{
    public MainContext(DbContextOptions<MainContext> options) : base(options)
    {
    }

    public DbSet<Card> Cards { get; set; }
    public DbSet<Collection> Collections { get; set; }
    public DbSet<Deck> Decks { get; set; }
    public DbSet<DeckCard> DeckCards { get; set; }
    public DbSet<Wishlist> Wishlists { get; set; }
    public DbSet<WishlistCard> WishlistCards { get; set; }
    public DbSet<TradeBinder> TradeBinders { get; set; }
    public DbSet<BinderCard> BinderCards { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
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
                    v => LanguageExtensions.ParseNullable(v));
        });

        modelBuilder.Entity<Card>(entity =>
        {
            entity.Property(e => e.Language)
                .HasConversion(
                    v => v.ToCode(),
                    v => LanguageExtensions.ParseOrDefault(v, Language.En));
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

    }
}
