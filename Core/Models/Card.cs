using System.ComponentModel.DataAnnotations;
using Core.Enums;

namespace Core.Models;

public class Card : BaseModel
{
    [MaxLength(200)]
    public required string Name { get; set; }
    [MaxLength(100)]
    public required string ScryfallId { get; set; }
    public int CollectionId { get; set; }
    public Collection? Collection { get; set; }
    public required int Quantity { get; set; }
    public required Language Language { get; set; }
    public Condition Condition  { get; set; }
    public required bool IsFoil { get; set; }
    public required double PurchasePrice { get; set; }
    public required Currency PurchasePriceCurrency { get; set; }
    [MaxLength(300)]
    public required string ImageUrl { get; set; }
    [MaxLength(300)]
    public string? BackImageUrl { get; set; }
    [MaxLength(300)]
    public required string ArtCrop { get; set; }
    [MaxLength(100)]
    public required string SetCode { get; set; }
    [MaxLength(100)]
    public required string SetName { get; set; }
    [MaxLength(200)]
    public required string TypeLine { get; set; }
    [MaxLength(100)]
    public required string CollectorNumber  { get; set; }
    [MaxLength(100)]
    public required string Rarity { get; set; }
    public required bool IsMisprint { get; set; }
    public required bool IsAltered  { get; set; }
}

public enum Condition
{
    Mint,
    NearMint,
    Excellent,
    Good,
    LightPlayed,
    Played,
    Poor
}
