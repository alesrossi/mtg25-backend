using System.Text;
using System.Text.Json;
using API.Configuration;
using API.Endpoints;
using API.Extensions;
using API.Helpers;
using API.Scryfall;
using API.Services;
using Core.Interfaces;
using Core.Models.Identity;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace API;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        // Bind the Scryfall configuration
        builder.Services.Configure<ScryfallConfig>(builder.Configuration.GetSection("Scryfall"));
        builder.Services.Configure<PathsConfig>(builder.Configuration.GetSection("Paths"));
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                // Enable built-in naming policies from System.Text.Json
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            });
        
        // Add services to the container.
        builder.Services.AddAuthorization();
        
        builder.Services.AddDbContext<MainContext>(options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            }
            options.UseNpgsql(connectionString);
        });
        
        builder.Services.AddDbContext<AppIdentityDbContext>(options =>
                {
                    var connectionString = builder.Configuration.GetConnectionString("IdentityConnection");
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        throw new InvalidOperationException("Connection string 'IdentityConnection' not found.");
                    }
                    options.UseNpgsql(connectionString);
                });

        
        builder.Services.Configure<JwtSettings>(
            builder.Configuration.GetSection("JWT"));
        
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = builder.Configuration.GetConnectionString("Redis");
            options.InstanceName = "MTG25";
        });
        
        
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        
        // Add CORS policy
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });
        
        // Add CardDataService as a singleton
        builder.Services.AddSingleton<CardDataService>();
        builder.Services.AddScoped<IJwtService, JwtService>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddIdentityServices(builder.Configuration);

        
        // JWT Configuration
        
        // Configure Authentication
        var jwtSettings = builder.Configuration.GetSection("JWT").Get<JwtSettings>()!;
        var key = Encoding.UTF8.GetBytes(jwtSettings.SecretKey);

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                // Custom event to check Redis blacklist
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var jwtService = context.HttpContext.RequestServices.GetRequiredService<IJwtService>();
                        var token = context.Request.Headers.Authorization
                            .ToString().Replace("Bearer ", "");

                        if (await jwtService.IsTokenBlacklistedAsync(token))
                        {
                            context.Fail("Token has been revoked");
                        }
                    }
                };
            });
        
        var app = builder.Build();
        
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            try
            {
                var context = services.GetRequiredService<MainContext>();
        
                // Check if database exists
                if (!await context.Database.CanConnectAsync())
                {
                    await context.Database.MigrateAsync();
                }
                else
                {
                    // Check if any pending migrations
                    if ((await context.Database.GetPendingMigrationsAsync()).Any())
                    {
                        await context.Database.MigrateAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "An error occurred while checking/applying migrations");
                throw;
            }
        }
        
        var cardDataService = app.Services.GetRequiredService<CardDataService>();
        await cardDataService.LoadCardDataAsync();

        
        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        
        app.UseAuthentication();
        app.UseAuthorization();
        
        app.UseCors();
        
        app.MapCardsEndpoints();
        app.MapAccountEndpoints();
        app.MapCollectionsEndpoints();
        app.MapDecksEndpoints();
        
        await DbHelpers.EnsureDatabasesCreated(app);
        
        app.Run();
    }
}