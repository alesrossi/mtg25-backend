using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Configuration;
using API.Endpoints;
using API.Extensions;
using API.Helpers;
using API.Services;
using Core.Interfaces;
using Core.Models.Identity;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Any;

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
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                options.JsonSerializerOptions.WriteIndented = true;
            });

        // Configure JSON for Minimal APIs
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
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
        
        builder.Services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var problemDetails = new ValidationProblemDetails(context.ModelState)
                {
                    Detail = "See errors for additional information.",
                    Instance = context.HttpContext.Request.Path,
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Request validation failed",
                    Type = "https://httpstatuses.io/400"
                };
                problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                var result = new BadRequestObjectResult(problemDetails);
                result.ContentTypes.Add("application/problem+json");

                return result;
            };
        });

// Add model validation
        builder.Services.AddScoped<IValidationService, ValidationService>();
        
        // Add CardDataService as a singleton
        builder.Services.AddSingleton<CardDataService>();
        builder.Services.AddScoped<IJwtService, JwtService>();
        builder.Services.AddScoped<DeckCardService>();
        builder.Services.AddScoped<IDeckValidationService, DeckValidationService>();
        builder.Services.AddScoped<IDecklistParserService, DecklistParserService>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddIdentityServices(builder.Configuration);

        
        // JWT Configuration
        
        // Configure Authentication
        var jwtSettings = builder.Configuration.GetSection("JWT").Get<JwtSettings>()!;
        // Only configure JWT if not in testing environment and JWT config is available
        if (jwtSettings != null && !builder.Environment.IsEnvironment("Testing"))
        {
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
        }
        
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
    
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme."
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            c.MapType<ProblemDetails>(() => new OpenApiSchema
            {
                Type = "object",
                Required = new HashSet<string> { "type", "title", "status", "traceId" },
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["type"] = new OpenApiSchema { Type = "string", Description = "Reference URI that identifies the problem type." },
                    ["title"] = new OpenApiSchema { Type = "string", Description = "Short, human-readable summary of the problem." },
                    ["status"] = new OpenApiSchema { Type = "integer", Format = "int32", Description = "HTTP status code for this occurrence." },
                    ["detail"] = new OpenApiSchema { Type = "string", Nullable = true, Description = "Detailed explanation helpful for debugging." },
                    ["instance"] = new OpenApiSchema { Type = "string", Nullable = true, Description = "The request path that produced the error." },
                    ["traceId"] = new OpenApiSchema { Type = "string", Description = "Server-generated trace identifier for correlating logs." },
                    ["errorCode"] = new OpenApiSchema { Type = "string", Nullable = true, Description = "Stable application-specific code describing the error." }
                },
                Example = new OpenApiObject
                {
                    ["type"] = new OpenApiString("https://httpstatuses.io/500"),
                    ["title"] = new OpenApiString("Card update failed"),
                    ["status"] = new OpenApiInteger(500),
                    ["detail"] = new OpenApiString("An unexpected error occurred while updating the card."),
                    ["instance"] = new OpenApiString("/api/cards/42"),
                    ["traceId"] = new OpenApiString("00-3d82f6cd2f0be945b27d1d7e7c92b5ce-949a13d67f0a3d43-00"),
                    ["errorCode"] = new OpenApiString("card-update-error")
                }
            });

            c.MapType<ValidationProblemDetails>(() => new OpenApiSchema
            {
                Type = "object",
                Required = new HashSet<string> { "type", "title", "status", "traceId", "errors" },
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["type"] = new OpenApiSchema { Type = "string" },
                    ["title"] = new OpenApiSchema { Type = "string" },
                    ["status"] = new OpenApiSchema { Type = "integer", Format = "int32" },
                    ["detail"] = new OpenApiSchema { Type = "string", Nullable = true },
                    ["instance"] = new OpenApiSchema { Type = "string", Nullable = true },
                    ["traceId"] = new OpenApiSchema { Type = "string" },
                    ["errors"] = new OpenApiSchema
                    {
                        Type = "object",
                        AdditionalProperties = new OpenApiSchema
                        {
                            Type = "array",
                            Items = new OpenApiSchema { Type = "string" }
                        },
                        Description = "Keyed collection containing validation messages per field."
                    }
                },
                Example = new OpenApiObject
                {
                    ["type"] = new OpenApiString("https://httpstatuses.io/400"),
                    ["title"] = new OpenApiString("Request validation failed"),
                    ["status"] = new OpenApiInteger(400),
                    ["detail"] = new OpenApiString("See errors for additional information."),
                    ["instance"] = new OpenApiString("/api/cards"),
                    ["traceId"] = new OpenApiString("00-7fdd0ff221c6cf438eab4b137eea521a-1cba7c30dad59c4e-00"),
                    ["errors"] = new OpenApiObject
                    {
                        ["quantity"] = new OpenApiArray
                        {
                            new OpenApiString("Quantity must be greater than zero.")
                        }
                    }
                }
            });
        });
        
        var app = builder.Build();
        
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            try
            {
                var context = services.GetRequiredService<MainContext>();
                await context.Database.MigrateAsync();

                var idContext = services.GetRequiredService<AppIdentityDbContext>();
                await idContext.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "An error occurred while checking/applying migrations");
                throw;
            }
        }
        
        if (!app.Environment.IsEnvironment("Testing"))
        {
            var cardDataService = app.Services.GetRequiredService<CardDataService>();
            await cardDataService.LoadCardDataAsync();
        }

        
        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // app.UseHttpsRedirection();
        
        app.UseAuthentication();
        app.UseAuthorization();
        
        app.UseCors();
        
        app.MapCardsEndpoints();
        app.MapAccountEndpoints();
        app.MapCollectionsEndpoints();
        app.MapBindersEndpoints();
        app.MapWishlistsEndpoints();
        app.MapLeaguesEndpoints();
        app.MapDecksEndpoints();
        
        // Add health check endpoint
        app.MapGet("/api/health", () => Results.Ok(new { 
            status = "healthy", 
            timestamp = DateTime.UtcNow,
            environment = app.Environment.EnvironmentName,
            version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        })).AllowAnonymous();

        // Add environment-specific configuration
        if (app.Environment.IsEnvironment("Integration"))
        {
            app.UseDeveloperExceptionPage();
            app.MapOpenApi();
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        
        app.Run();
    }
}
