using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Core.Models;

namespace Core.Specifications;

public sealed class BinderCardsByBinderIdsSpecification : BaseSpecification<BinderCard>
{
    public BinderCardsByBinderIdsSpecification(IEnumerable<int> binderIds)
        : base(BuildCriteria(binderIds))
    {
        AddInclude(card => card.Card);
    }

    private static Expression<Func<BinderCard, bool>> BuildCriteria(IEnumerable<int> binderIds)
    {
        var uniqueIds = binderIds?.Distinct().ToArray() ?? Array.Empty<int>();
        if (uniqueIds.Length == 0)
        {
            return card => false;
        }

        return card => uniqueIds.Contains(card.TradeBinderId);
    }
}
