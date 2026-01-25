using System;
using System.Collections.Generic;
using Core.Enums;
using Core.Models;

namespace Core.Specifications;

public class EntitySpecParams
{
    private const int MaxPageSize = 50;
    public int PageIndex { get; set; } = 1;
    private int _pageSize = 6;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = (value > MaxPageSize) ? MaxPageSize : value;
    }
    public string? Sort { get; set; }
    public string? Search { get; set; }
    public string? SetCode { get; set; }
    public string? SetName { get; set; }
    public string? Rarity { get; set; }
    public Condition? Condition { get; set; }
    public bool? IsFoil { get; set; }
    public bool? IsMisprint { get; set; }
    public bool? IsAltered { get; set; }
    public Language? Language { get; set; }
    public double? MinPrice { get; set; }
    public double? MaxPrice { get; set; }
    public string? GroupBy { get; set; }

    private static readonly HashSet<string> AllowedTypeLines = new(
        new[] { "Artifact", "Creature", "Enchantment", "Land", "Instant", "Sorcery", "Planeswalker", "Battle" },
        StringComparer.OrdinalIgnoreCase);

    private string? typeLine;
    public string? TypeLine
    {
        get => typeLine;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                typeLine = null;
                return;
            }

            foreach (var allowed in AllowedTypeLines)
            {
                if (allowed.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    typeLine = allowed;
                    return;
                }
            }

            typeLine = null;
        }
    }

    public static IReadOnlyCollection<string> GetAllowedTypeLines() => AllowedTypeLines;
}
