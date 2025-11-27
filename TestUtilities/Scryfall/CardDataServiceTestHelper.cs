using System;
using System.Collections.Generic;
using System.Linq;
using API.Configuration;
using API.Dtos.Cards;
using API.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace TestUtilities.Scryfall;

public static class CardDataServiceTestHelper
{
    public static CardDataService CreateWithCards(IEnumerable<OracleCardDto> cards)
    {
        var service = new CardDataService(
            Options.Create(new PathsConfig()),
            Options.Create(new ScryfallConfig()),
            NullLogger<CardDataService>.Instance);

        Populate(service, cards);
        return service;
    }

    public static void Populate(CardDataService service, IEnumerable<OracleCardDto> cards)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(cards);

        var cardList = cards.ToList();
        var cardsById = cardList.ToDictionary(c => c.Id);
        var cardsByName = cardList.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

        typeof(CardDataService).GetProperty(nameof(CardDataService.CardDataById))!
            .SetValue(service, cardsById);
        typeof(CardDataService).GetProperty(nameof(CardDataService.CardDataByName))!
            .SetValue(service, cardsByName);
    }
}
