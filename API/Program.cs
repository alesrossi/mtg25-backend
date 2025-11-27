using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Configuration;
using API.Endpoints;
using API.Extensions;
using API.Filters;
using API.Helpers;
using API.Services;
using Core.Interfaces;
using Core.Models.Identity;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Any;
using System.IO.Compression;
using Serilog;

namespace API;

public class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting MTG25 host");

            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((context, services, loggerConfiguration) =>
            {
                loggerConfiguration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext();
            });

            // Add services to the container.
            // Bind the Scryfall configuration
            builder.Services.Configure<ScryfallConfig>(builder.Configuration.GetSection("Scryfall"));
            builder.Services.Configure<PathsConfig>(builder.Configuration.GetSection("Paths"));
            builder.Services.Configure<RequestLoggingOptions>(builder.Configuration.GetSection("RequestLogging"));
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
            builder.Services.AddScoped<ProblemDetailsEndpointFilter>();
            builder.Services.AddScoped<IJwtService, JwtService>();
            builder.Services.AddScoped<DeckCardService>();
            builder.Services.AddScoped<IDeckValidationService, DeckValidationService>();
            builder.Services.AddScoped<IDecklistParserService, DecklistParserService>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddIdentityServices(builder.Configuration);

            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Clear();
                options.Providers.Add<BrotliCompressionProvider>();
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
                {
                    "application/json",
                    "application/problem+json"
                });
            });

            builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Fastest;
            });

            
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
                            },
                            OnChallenge = challengeContext =>
                            {
                                challengeContext.HandleResponse();
                                var result = ProblemResultFactory.Create(
                                    challengeContext.HttpContext,
                                    StatusCodes.Status401Unauthorized,
                                "Authentication required",
                                "You must be logged in to access this resource.",
                                "auth-required");
                            return result.ExecuteAsync(challengeContext.HttpContext);
                        },
                        OnForbidden = forbiddenContext =>
                        {
                            var result = ProblemResultFactory.Create(
                                forbiddenContext.HttpContext,
                                StatusCodes.Status403Forbidden,
                                "Forbidden",
                                "You do not have permission to access this resource.",
                                "auth-forbidden");
                            return result.ExecuteAsync(forbiddenContext.HttpContext);
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
                Description = "Standard RFC 7807 envelope used for authentication, authorization, not found, and server errors.",
                Required = new HashSet<string> { "type", "title", "status", "traceId" },
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["type"] = new OpenApiSchema { Type = "string", Description = "Reference URI that identifies the problem type (e.g. https://httpstatuses.io/404)." },
                    ["title"] = new OpenApiSchema { Type = "string", Description = "Short, human-readable summary of the problem." },
                    ["status"] = new OpenApiSchema { Type = "integer", Format = "int32", Description = "HTTP status code for this occurrence." },
                    ["detail"] = new OpenApiSchema { Type = "string", Nullable = true, Description = "Detailed explanation helpful for debugging." },
                    ["instance"] = new OpenApiSchema { Type = "string", Nullable = true, Description = "The request path that produced the error." },
                    ["traceId"] = new OpenApiSchema { Type = "string", Description = "Server-generated trace identifier for correlating logs." },
                    ["errorCode"] = new OpenApiSchema { Type = "string", Nullable = true, Description = "Stable application-specific code describing the error." }
                },
                Example = new OpenApiObject
                {
                    ["type"] = new OpenApiString("https://httpstatuses.io/{status}"),
                    ["title"] = new OpenApiString("Meaningful summary of the failure"),
                    ["status"] = new OpenApiInteger(0),
                    ["detail"] = new OpenApiString("Optional human-readable detail about what went wrong."),
                    ["instance"] = new OpenApiString("/api/resource/{id}"),
                    ["traceId"] = new OpenApiString("00-TRACE_ID-HERE-00"),
                    ["errorCode"] = new OpenApiString("application-specific-code")
                }
            });
            
            c.MapType<UnauthorizedResult>(() => new OpenApiSchema
            {
                Type = "object",
                Description = "Standard RFC 7807 envelope used for 401, 404, and 500 errors.",
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
                    ["type"] = new OpenApiString("https://httpstatuses.io/401"),
                    ["title"] = new OpenApiString("Unauthorized user access"),
                    ["status"] = new OpenApiInteger(401),
                    ["detail"] = new OpenApiString("Jwt token either missing, invalid or expired"),
                    ["instance"] = new OpenApiString("/api/cards/42"),
                    ["traceId"] = new OpenApiString("00-3d82f6cd2f0be945b27d1d7e7c92b5ce-949a13d67f0a3d43-00"),
                    ["errorCode"] = new OpenApiString("card-unauthorized-error")
                }
            });

            c.MapType<ValidationProblemDetails>(() => new OpenApiSchema
            {
                Type = "object",
                Description = "RFC 7807 validation payload emitted for 400 Bad Request responses.",
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
        
        app.UseRequestLogging();
        app.UseResponseCompression();

        app.UseAuthentication();
        app.UseAuthorization();
        
        app.Use(async (context, next) =>
        {
            await next();

            if (context.Response.StatusCode == StatusCodes.Status404NotFound &&
                !context.Response.HasStarted &&
                context.GetEndpoint() is null)
            {
                var result = ProblemResultFactory.Create(
                    context,
                    StatusCodes.Status404NotFound,
                    "Endpoint not found",
                    $"No endpoint matches '{context.Request.Path}'.",
                    "endpoint-not-found");

                await result.ExecuteAsync(context);
            }
        });

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
        
        await app.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unhandled exception during MTG25 host execution");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
