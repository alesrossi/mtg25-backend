using Core.Models;

namespace Core.Specifications;

public class TradeBinderWithCardsSpecification : BaseSpecification<TradeBinder>
{
    public TradeBinderWithCardsSpecification(int binderId)
        : base(binder => binder.Id == binderId)
    {
        AddInclude(b => b.BinderCards);
    }

    public TradeBinderWithCardsSpecification(int binderId, string ownerId)
        : base(binder => binder.Id == binderId && binder.OwnerId == ownerId)
    {
        AddInclude(b => b.BinderCards);
    }
}
