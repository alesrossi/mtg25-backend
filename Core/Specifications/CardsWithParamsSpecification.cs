using Core.Models;

namespace Core.Specifications;

public class CardsWithParamsSpecification : BaseSpecification<Card>
{
    public CardsWithParamsSpecification(EntitySpecParams entityParams, int collectionId)
        : base(x =>
            (string.IsNullOrEmpty(entityParams.Search) || x.Name.Contains(entityParams.Search)) &&
            // Add other filtering logic here
            x.CollectionId == collectionId)

    {
        AddOrderBy(x => x.Name);
        ApplyPaging(entityParams.PageSize * (entityParams.PageIndex - 1), entityParams.PageSize);

        if (!string.IsNullOrEmpty(entityParams.Sort))
        {
            switch (entityParams.Sort)
            {
                case "priceAsc":
                    AddOrderBy(p => p.PurchasePrice);
                    break;
                case "priceDesc":
                    AddOrderByDescending(p => p.PurchasePrice);
                    break;
                default:
                    AddOrderBy(n => n.Name);
                    break;
            }
        }
    }

    public CardsWithParamsSpecification(int id) : base(x => x.Id == id)
    {
    }
}