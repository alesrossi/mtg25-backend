using Core.Models;
#pragma warning disable CS8603 // Possible null reference return.

namespace Core.Specifications;

public class DeckCardsWithScryfallIdSpecification : BaseSpecification<DeckCard>
{
    public DeckCardsWithScryfallIdSpecification(string scryfallId) 
        : base(dc => dc.ScryfallId == scryfallId)
    {
        AddInclude(dc => dc.Deck);
        AddInclude(dc => dc.OwnedCard);
        AddOrderBy(dc => dc.Name);
    }

    public DeckCardsWithScryfallIdSpecification(string scryfallId, int deckId) 
        : base(dc => dc.ScryfallId == scryfallId && dc.DeckId == deckId)
    {
        AddInclude(dc => dc.Deck);
        AddInclude(dc => dc.OwnedCard);
        AddOrderBy(dc => dc.Name);
    }

    public DeckCardsWithScryfallIdSpecification(string scryfallId, string userId) 
        : base(dc => dc.ScryfallId == scryfallId && dc.Deck.OwnerId == userId)
    {
        AddInclude(dc => dc.Deck);
        AddInclude(dc => dc.OwnedCard);
        AddOrderBy(dc => dc.Name);
    }
}