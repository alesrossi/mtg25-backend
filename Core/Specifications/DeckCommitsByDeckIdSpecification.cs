using Core.Models;

namespace Core.Specifications;

public class DeckCommitsByDeckIdSpecification : BaseSpecification<DeckCommit>
{
    public DeckCommitsByDeckIdSpecification(int deckId)
        : base(commit => commit.DeckId == deckId)
    {
        AddOrderByDescending(commit => commit.CommittedAt);
    }
}
