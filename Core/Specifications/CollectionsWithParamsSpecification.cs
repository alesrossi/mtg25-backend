using Core.Models;

namespace Core.Specifications;

public class CollectionsWithParamsSpecification : BaseSpecification<Collection>
{
    public CollectionsWithParamsSpecification(EntitySpecParams entityParams, string userId)
        : base(x =>
            //(string.IsNullOrEmpty(entityParams.Search) || x.Name.Contains(entityParams.Search)) &&
            // Add other filtering logic here
            x.OwnerId == userId)

    {
        
    }

    public CollectionsWithParamsSpecification(int id) : base(x => x.Id == id)
    {
    }
}