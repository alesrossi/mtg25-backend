using System.Linq.Expressions;
using Core.Models;

namespace Core.Specifications;

public class CardsWithParamsSpecification : BaseSpecification<Card>
{
    public CardsWithParamsSpecification(
        EntitySpecParams entityParams,
        int collectionId,
        bool applySorting = true,
        bool applyPaging = true)
        : base(BuildCriteria(entityParams, collectionId))

    {
        if (applySorting)
        {
            ApplySorting(entityParams.Sort);
        }

        if (applyPaging)
        {
            ApplyPaging(entityParams.PageSize * (entityParams.PageIndex - 1), entityParams.PageSize);
        }
    }

    public CardsWithParamsSpecification(int id) : base(x => x.Id == id)
    {
    }

    private void ApplySorting(string? sort)
    {
        if (string.IsNullOrEmpty(sort))
        {
            AddOrderBy(n => n.Name);
            return;
        }

        switch (sort)
        {
            case "priceAsc":
                AddOrderBy(p => p.PurchasePrice);
                break;
            case "priceDesc":
                AddOrderByDescending(p => p.PurchasePrice);
                break;
            case "nameAsc":
                AddOrderBy(n => n.Name);
                break;
            case "nameDesc":
                AddOrderByDescending(n => n.Name);
                break;
            case "setAsc":
                AddOrderBy(s => s.SetName);
                break;
            case "setDesc":
                AddOrderByDescending(s => s.SetName);
                break;
            case "rarityAsc":
                AddOrderBy(r =>
                    r.Rarity.ToLower() == "common" ? 0 :
                    r.Rarity.ToLower() == "uncommon" ? 1 :
                    r.Rarity.ToLower() == "rare" ? 2 :
                    r.Rarity.ToLower() == "mythic" ? 3 :
                    r.Rarity.ToLower() == "special" ? 4 :
                    5);
                break;
            case "rarityDesc":
                AddOrderByDescending(r =>
                    r.Rarity.ToLower() == "common" ? 0 :
                    r.Rarity.ToLower() == "uncommon" ? 1 :
                    r.Rarity.ToLower() == "rare" ? 2 :
                    r.Rarity.ToLower() == "mythic" ? 3 :
                    r.Rarity.ToLower() == "special" ? 4 :
                    -1);
                break;
            case "typeAsc":
                AddOrderBy(t => t.TypeLine);
                break;
            case "typeDesc":
                AddOrderByDescending(t => t.TypeLine);
                break;
            case "quantityAsc":
                AddOrderBy(q => q.Quantity);
                break;
            case "quantityDesc":
                AddOrderByDescending(q => q.Quantity);
                break;
            case "currentPriceAsc":
            case "currentPriceDesc":
                break;
            default:
                AddOrderBy(n => n.Name);
                break;
        }
    }

    private static Expression<Func<Card, bool>> BuildCriteria(EntitySpecParams entityParams, int collectionId)
    {
        var searchTerm = entityParams.Search?.ToLowerInvariant();
        var setNameTerm = entityParams.SetName?.ToLowerInvariant();
        var typeLineTerm = entityParams.TypeLine?.ToLowerInvariant();

        return x =>
            (string.IsNullOrEmpty(searchTerm) || x.Name.ToLower().Contains(searchTerm)) &&
            (string.IsNullOrEmpty(entityParams.SetCode) || x.SetCode == entityParams.SetCode) &&
            (string.IsNullOrEmpty(setNameTerm) || x.SetName.ToLower().Contains(setNameTerm)) &&
            (string.IsNullOrEmpty(entityParams.Rarity) || x.Rarity == entityParams.Rarity) &&
            (entityParams.Condition == null || x.Condition == entityParams.Condition) &&
            (entityParams.IsFoil == null || x.IsFoil == entityParams.IsFoil) &&
            (entityParams.IsMisprint == null || x.IsMisprint == entityParams.IsMisprint) &&
            (entityParams.IsAltered == null || x.IsAltered == entityParams.IsAltered) &&
            (entityParams.Language == null || x.Language == entityParams.Language) &&
            (string.IsNullOrEmpty(typeLineTerm) || x.TypeLine.ToLower().Contains(typeLineTerm)) &&
            (entityParams.MinPrice == null || x.PurchasePrice >= entityParams.MinPrice) &&
            (entityParams.MaxPrice == null || x.PurchasePrice <= entityParams.MaxPrice) &&
            x.CollectionId == collectionId;
    }
}
