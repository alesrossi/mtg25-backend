using System.Net;
using System.Net.Http.Headers;
using API.Dtos;
using Core.Models;

namespace API.Scryfall;

public static class ScryfallApiCalls
{
    private static readonly Uri BaseUrl = new Uri("https://api.scryfall.com/");
    
}