using Core.Models;

namespace Core.Specifications;

public class WishlistsWithOwnerSpecification : BaseSpecification<Wishlist>
{
    public WishlistsWithOwnerSpecification(string ownerId, bool includeCards = false)
        : base(wishlist => wishlist.OwnerId == ownerId)
    {
        if (includeCards)
        {
            AddInclude(w => w.WishlistCards);
        }

        AddOrderBy(w => w.Name);
    }
}
