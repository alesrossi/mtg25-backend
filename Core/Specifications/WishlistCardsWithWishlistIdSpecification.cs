using Core.Models;

namespace Core.Specifications;

public class WishlistCardsWithWishlistIdSpecification : BaseSpecification<WishlistCard>
{
    public WishlistCardsWithWishlistIdSpecification(int wishlistId)
        : base(card => card.WishlistId == wishlistId)
    {
        AddOrderBy(c => c.Name);
    }
}
