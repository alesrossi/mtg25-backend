using API.Dtos.Cards;
using Core.Models;
using Core.Models.Identity;

namespace API.Endpoints.Cards;

public static class CardsEndpointsHelpers
{
    public static ExtensiveCardDto MapToDto(Card card, double? price, MarketProvider? priceCurrency)
    {
        return new ExtensiveCardDto
        {
            Id = card.Id,
            Name = card.Name,
            ScryfallId = card.ScryfallId,
            CollectionId = card.CollectionId,
            Quantity = card.Quantity,
            Language = card.Language,
            Condition = card.Condition,
            IsFoil = card.IsFoil,
            PurchasePrice = card.PurchasePrice,
            PurchasePriceCurrency = card.PurchasePriceCurrency,
            ImageUrl = card.ImageUrl,
            BackImageUrl = card.BackImageUrl,
            ArtCrop = card.ArtCrop,
            SetCode = card.SetCode,
            SetName = card.SetName,
            TypeLine = card.TypeLine,
            CollectorNumber = card.CollectorNumber,
            Rarity = card.Rarity,
            IsMisprint = card.IsMisprint,
            IsAltered = card.IsAltered,
            Price = price,
            PriceCurrency = priceCurrency
        };
    }
}
