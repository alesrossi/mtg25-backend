using System.Linq;
using System.Linq.Expressions;
using Core.Models;

namespace Core.Specifications;

public class CardsByIdsSpecification : BaseSpecification<Card>
{
    public CardsByIdsSpecification(IEnumerable<int> cardIds, int? collectionId = null)
        : base(BuildCriteria(cardIds, collectionId))
    {
    }

    private static Expression<Func<Card, bool>> BuildCriteria(IEnumerable<int> cardIds, int? collectionId)
    {
        var ids = cardIds.Distinct().ToArray();

        if (collectionId.HasValue)
        {
            var collection = collectionId.Value;
            return card => ids.Contains(card.Id) && card.CollectionId == collection;
        }

        return card => ids.Contains(card.Id);
    }
}
