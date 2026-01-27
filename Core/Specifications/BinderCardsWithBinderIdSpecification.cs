using Core.Models;

namespace Core.Specifications;

public class BinderCardsWithBinderIdSpecification : BaseSpecification<BinderCard>
{
    public BinderCardsWithBinderIdSpecification(int binderId)
        : base(card => card.TradeBinderId == binderId)
    {
        AddInclude(card => card.Card!);
        AddOrderBy(card => card.Name);
    }
}
