using System.Reflection;
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
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Collection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();
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
            entity.Property(dc => dc.OracleId).IsRequired();
            entity.Property(dc => dc.Name).IsRequired();
            entity.Property(dc => dc.SetCode).IsRequired();
        });

    }
}