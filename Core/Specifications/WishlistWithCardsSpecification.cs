using Core.Models;

namespace Core.Specifications;

public class WishlistWithCardsSpecification : BaseSpecification<Wishlist>
{
    public WishlistWithCardsSpecification(int wishlistId, string ownerId)
        : base(w => w.Id == wishlistId && w.OwnerId == ownerId)
    {
        AddInclude(w => w.WishlistCards);
    }
}
