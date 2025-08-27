using Core.Models;

namespace Core.Specifications;

public class DecksWIthOwnerSpecification(string userId) : BaseSpecification<Deck>(x =>
    x.OwnerId == userId);