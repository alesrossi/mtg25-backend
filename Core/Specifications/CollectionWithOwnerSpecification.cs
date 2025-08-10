using Core.Models;

namespace Core.Specifications;


public class CollectionWithOwnerSpecification : BaseSpecification<Collection>
{
    public CollectionWithOwnerSpecification(string userId)
        : base(x =>
            x.OwnerId == userId)

    {
    }


}
