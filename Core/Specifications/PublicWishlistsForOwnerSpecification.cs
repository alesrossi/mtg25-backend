using Core.Models;

namespace Core.Specifications;

public sealed class PublicWishlistsForOwnerSpecification : BaseSpecification<Wishlist>
{
    public PublicWishlistsForOwnerSpecification(string ownerId, bool includeCards = false)
        : base(wishlist => wishlist.OwnerId == ownerId && wishlist.IsPublic)
    {
        if (includeCards)
        {
            AddInclude(w => w.WishlistCards);
        }

        AddOrderBy(w => w.Name);
    }
}
