using Core.Models;

namespace Core.Specifications;

public class DeckCardsWithDeckIdSpecification : BaseSpecification<DeckCard>
{
    public DeckCardsWithDeckIdSpecification(int deckId) 
        : base(dc => dc.DeckId == deckId)
    {
        AddInclude(dc => dc.Deck);
        AddInclude(dc => dc.OwnedCard);
        AddOrderBy(dc => dc.Name);
    }

    public DeckCardsWithDeckIdSpecification(int deckId, bool maindeckOnly) 
        : base(dc => dc.DeckId == deckId && (!maindeckOnly || dc.MaindeckQuantity > 0))
    {
        AddInclude(dc => dc.Deck);
        AddInclude(dc => dc.OwnedCard);
        AddOrderBy(dc => dc.Name);
    }

    public DeckCardsWithDeckIdSpecification(int deckId, bool maindeckOnly, bool sideboardOnly) 
        : base(dc => dc.DeckId == deckId && 
                     (!maindeckOnly || dc.MaindeckQuantity > 0) &&
                     (!sideboardOnly || dc.SideboardQuantity > 0))
    {
        AddInclude(dc => dc.Deck);
        AddInclude(dc => dc.OwnedCard);
        AddOrderBy(dc => dc.Name);
    }
}