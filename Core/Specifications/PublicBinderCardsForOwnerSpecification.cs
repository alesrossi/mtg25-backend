using Core.Models;
#pragma warning disable CS8603 // Possible null reference return.

namespace Core.Specifications;

public sealed class PublicBinderCardsForOwnerSpecification : BaseSpecification<BinderCard>
{
    public PublicBinderCardsForOwnerSpecification(string ownerId)
        : base(card => card.TradeBinder.OwnerId == ownerId && card.TradeBinder.IsPublic)
    {
        AddInclude(card => card.TradeBinder);
        AddInclude(card => card.Card);
    }
}
