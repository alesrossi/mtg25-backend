using Core.Models;

namespace Core.Specifications;


public class CollectionWithOwnerSpecification(string userId) : BaseSpecification<Collection>(x =>
    x.OwnerId == userId);
