using Core.Models;

namespace Core.Specifications;

public class DeckTreeEntriesByTreeIdSpecification : BaseSpecification<DeckTreeEntry>
{
    public DeckTreeEntriesByTreeIdSpecification(int treeId)
        : base(entry => entry.TreeId == treeId)
    {
        AddOrderBy(entry => entry.ScryfallId);
    }
}
