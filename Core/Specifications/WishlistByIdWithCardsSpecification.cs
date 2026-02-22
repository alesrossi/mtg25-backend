using Core.Models;

namespace Core.Specifications;

public class WishlistByIdWithCardsSpecification : BaseSpecification<Wishlist>
{
    public WishlistByIdWithCardsSpecification(int wishlistId)
        : base(w => w.Id == wishlistId)
    {
        AddInclude(w => w.WishlistCards);
    }
}
