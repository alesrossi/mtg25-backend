using Core.Models;

namespace Core.Specifications;

public class TradeBindersWithOwnerSpecification : BaseSpecification<TradeBinder>
{
    public TradeBindersWithOwnerSpecification(string ownerId, bool includeCards = false)
        : base(binder => binder.OwnerId == ownerId)
    {
        if (includeCards)
        {
            AddInclude(b => b.BinderCards);
        }

        AddOrderBy(b => b.Name);
    }
}
