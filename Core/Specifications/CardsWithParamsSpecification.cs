using Core.Models;

namespace Core.Specifications;

public class CardsWithParamsSpecification : BaseSpecification<Card>
{
    public CardsWithParamsSpecification(CardsSpecParams cardParams, int collectionId)
        : base(x =>
            (string.IsNullOrEmpty(cardParams.Search) || x.Name.Contains(cardParams.Search)) &&
            // Add other filtering logic here
            x.CollectionId == collectionId)

    {
        AddOrderBy(x => x.Name);
        ApplyPaging(cardParams.PageSize * (cardParams.PageIndex - 1), cardParams.PageSize);

        if (!string.IsNullOrEmpty(cardParams.Sort))
        {
            switch (cardParams.Sort)
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