using Core.Models;

namespace Core.Specifications;

public class BinderCardWithBinderSpecification : BaseSpecification<BinderCard>
{
    public BinderCardWithBinderSpecification(int binderCardId)
        : base(card => card.Id == binderCardId)
    {
        AddInclude(card => card.TradeBinder);
        AddInclude(card => card.Card);
        AddInclude(card => card.Card!.Collection!);
    }
}
