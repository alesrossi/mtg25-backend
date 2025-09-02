using Core.Models;

namespace Core.Specifications;

public class DeckCardsWithOracleIdSpecification : BaseSpecification<DeckCard>
{
    public DeckCardsWithOracleIdSpecification(string oracleId) 
        : base(dc => dc.OracleId == oracleId)
    {
        AddInclude(dc => dc.Deck);
        AddInclude(dc => dc.OwnedCard);
        AddOrderBy(dc => dc.Name);
    }

    public DeckCardsWithOracleIdSpecification(string oracleId, int deckId) 
        : base(dc => dc.OracleId == oracleId && dc.DeckId == deckId)
    {
        AddInclude(dc => dc.Deck);
        AddInclude(dc => dc.OwnedCard);
        AddOrderBy(dc => dc.Name);
    }

    public DeckCardsWithOracleIdSpecification(string oracleId, string userId) 
        : base(dc => dc.OracleId == oracleId && dc.Deck.OwnerId == userId)
    {
        AddInclude(dc => dc.Deck);
        AddInclude(dc => dc.OwnedCard);
        AddOrderBy(dc => dc.Name);
    }
}