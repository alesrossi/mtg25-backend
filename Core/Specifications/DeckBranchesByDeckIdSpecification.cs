using Core.Models;

namespace Core.Specifications;

public class DeckBranchesByDeckIdSpecification : BaseSpecification<DeckBranch>
{
    public DeckBranchesByDeckIdSpecification(int deckId)
        : base(branch => branch.DeckId == deckId)
    {
        AddOrderBy(branch => branch.Name);
    }
}
