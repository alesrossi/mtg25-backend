using Core.Models;

namespace Core.Specifications;

public sealed class CardsForCollectionSpecification : BaseSpecification<Card>
{
    public CardsForCollectionSpecification(int collectionId)
        : base(card => card.CollectionId == collectionId)
    {
    }
}
