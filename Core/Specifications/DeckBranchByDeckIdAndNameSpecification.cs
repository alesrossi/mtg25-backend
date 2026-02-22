using Core.Models;

namespace Core.Specifications;

public class DeckBranchByDeckIdAndNameSpecification : BaseSpecification<DeckBranch>
{
    public DeckBranchByDeckIdAndNameSpecification(int deckId, string branchName)
        : base(branch => branch.DeckId == deckId && branch.Name == branchName)
    {
    }
}
