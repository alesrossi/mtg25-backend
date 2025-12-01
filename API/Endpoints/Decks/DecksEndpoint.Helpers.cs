using API.Dtos.Decks;
using Core.Models;

namespace API.Endpoints.Decks;

public static partial class DecksEndpoint
{
    private static DeckDto MapToDto(Deck deck)
    {
        return new DeckDto
        {
            Id = deck.Id,
            Name = deck.Name,
            Format = deck.Format,
            Image = deck.Image,
            NumberOfCards = deck.NumberOfCards,
            NumberOfMainBoardCards = deck.NumberOfMainBoardCards,
            NumberOfSideBoardCards = deck.NumberOfSideBoardCards,
            TotalPrice = deck.TotalPrice,
            ColorIdentity = deck.ColorIdentity.ToList(),
            OwnerId = deck.OwnerId
        };
    }
}
