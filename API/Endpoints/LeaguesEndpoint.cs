using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using API.Dtos;
using Core.Models.Identity;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Endpoints;

public static class LeaguesEndpoint
{
    public static void MapLeaguesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/leagues").WithTags("Leagues");
        group.MapGet("/user", GetLeaguesFromUser)
            .WithSummary("Returns all leagues for user")
            .WithDescription("Returns all leagues for user given JWT token");
    }
    
    private static async Task<IResult> GetLeaguesFromUser(
        [FromServices] UserManager<AppUser> userManager, 
        [FromServices] AppIdentityDbContext  dbContext,
        HttpContext context)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Results.Unauthorized();
        
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Results.Unauthorized();
        
        var res = await dbContext.UserLeagues
            .Where(ul => ul.UserId == userId)
            .Include(ul => ul.League)
            .ToListAsync();

        List<LeagueDto> leaguesDto = [];
        res.ForEach(x => 
            leaguesDto.Add(new LeagueDto
            {
                Code = x.League.Code, 
                Name = x.League.Name, 
                Score = x.Score, 
                Id = x.LeagueId,
                EndDate = x.League.EndDate,
                Format = x.League.Format,
            }));
        
        return Results.Ok(new UserWithLeaguesDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = user.DisplayName,
            Leagues = leaguesDto
        });
    }
    
    private static class SecureCodeGenerator
    {
        private static readonly char[] chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();
    
        public static string GenerateCode(int length = 6)
        {
            var data = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(data);
            }

            var result = new StringBuilder(length);
            foreach (var b in data)
            {
                result.Append(chars[b % chars.Length]);
            }

            return result.ToString();
        }
    }
}