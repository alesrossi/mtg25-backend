using System;

namespace API.Helpers;

public static class CollectionValueCalculator
{
    public static double CalculateCardValue(double purchasePrice, int quantity)
    {
        var safeQuantity = Math.Max(0, quantity);
        if (safeQuantity == 0 || purchasePrice <= 0)
        {
            return 0;
        }

        var safePrice = Math.Max(0, purchasePrice);
        return Math.Round(safePrice * safeQuantity, 2, MidpointRounding.AwayFromZero);
    }

    public static double ApplyTotalPriceDelta(double currentTotal, double delta)
    {
        var updatedTotal = Math.Max(0, currentTotal + delta);
        return Math.Round(updatedTotal, 2, MidpointRounding.AwayFromZero);
    }
}
