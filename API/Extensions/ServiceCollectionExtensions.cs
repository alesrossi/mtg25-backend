using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Configuration;
using API.Filters;
using API.Helpers;
using API.Json;
using API.Services;
using Core.Interfaces;
using Core.Models.Identity;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace API.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApiConfiguration(IConfiguration configuration)
        {
            services.Configure<ScryfallConfig>(configuration.GetSection("Scryfall"));
            services.Configure<PathsConfig>(configuration.GetSection("Paths"));
            services.Configure<RequestLoggingOptions>(configuration.GetSection("RequestLogging"));
            services.Configure<JwtSettings>(configuration.GetSection("JWT"));

            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                    options.JsonSerializerOptions.WriteIndented = true;
                    options.JsonSerializerOptions.Converters.Add(new LanguageJsonConverter());
                    options.JsonSerializerOptions.Converters.Add(new NullableLanguageJsonConverter());
                    options.JsonSerializerOptions.Converters.Add(new DeckFormatJsonConverter());
                    options.JsonSerializerOptions.Converters.Add(new NullableDeckFormatJsonConverter());
                    options.JsonSerializerOptions.Converters.Add(new CurrencyJsonConverter());
                    options.JsonSerializerOptions.Converters.Add(new NullableCurrencyJsonConverter());
                });

            services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                options.SerializerOptions.Converters.Add(new LanguageJsonConverter());
                options.SerializerOptions.Converters.Add(new NullableLanguageJsonConverter());
                options.SerializerOptions.Converters.Add(new DeckFormatJsonConverter());
                options.SerializerOptions.Converters.Add(new NullableDeckFormatJsonConverter());
                options.SerializerOptions.Converters.Add(new CurrencyJsonConverter());
                options.SerializerOptions.Converters.Add(new NullableCurrencyJsonConverter());
            });

            services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var problemDetails = new ValidationProblemDetails(context.ModelState)
                    {
                        Detail = "See errors for additional information.",
                        Instance = context.HttpContext.Request.Path,
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Request validation failed",
                        Type = "https://httpstatuses.io/400",
                        Extensions =
                        {
                            ["traceId"] = context.HttpContext.TraceIdentifier
                        }
                    };

                    var result = new BadRequestObjectResult(problemDetails);
                    result.ContentTypes.Add("application/problem+json");

                    return result;
                };
            });

            services.AddLocalization(options => options.ResourcesPath = "Resources");

            return services;
        }

        public IServiceCollection AddDatabaseContexts(IConfiguration configuration)
        {
            services.AddDbContext<MainContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
                }
                options.UseNpgsql(connectionString);
            });

            services.AddDbContext<AppIdentityDbContext>(options =>
            {
                var connectionString = configuration.GetConnectionString("IdentityConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("Connection string 'IdentityConnection' not found.");
                }
                options.UseNpgsql(connectionString);
            });

            return services;
        }

        public IServiceCollection AddCaching(IConfiguration configuration)
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration.GetConnectionString("Redis");
                options.InstanceName = "MTG25";
            });

            return services;
        }

        public IServiceCollection AddApiSwagger()
        {
            services.AddOpenApi();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
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
            });

            return services;
        }

        public IServiceCollection AddApiCors(IConfiguration configuration)
        {
            services.AddCors(options =>
            {
                var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
                options.AddPolicy("DefaultCors", policy =>
                {
                    if (allowedOrigins.Length == 0)
                    {
                        policy.SetIsOriginAllowed(_ => false);
                    }
                    else
                    {
                        policy.WithOrigins(allowedOrigins)
                            .AllowAnyMethod()
                            .AllowAnyHeader();
                    }
                });
            });

            return services;
        }

        public IServiceCollection AddApiServices(IConfiguration configuration)
        {
            services.AddScoped<IValidationService, ValidationService>();
            services.AddSingleton<CardDataService>();
            services.AddScoped<ProblemDetailsEndpointFilter>();
            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<DeckCardService>();
            services.AddScoped<WishlistPricingService>();
            services.AddScoped<IUserSettingsService, UserSettingsService>();
            services.AddScoped<ICollectionService, CollectionService>();
            services.AddScoped<ITradeSessionStore, TradeSessionStore>();
            services.AddScoped<ITradeConnectionService, TradeConnectionService>();
            services.AddScoped<IFriendService, FriendService>();
            services.AddScoped<ILeagueService, LeagueService>();
            services.AddScoped<IWishlistService, WishlistService>();
            services.AddScoped<ICardsService, CardsService>();
            services.AddScoped<IBindersService, BindersService>();
            services.AddScoped<IMessageLocalizer, MessageLocalizationService>();
            services.AddScoped<NotificationService>();
            services.AddScoped<IDeckValidationService, DeckValidationService>();
            services.AddScoped<IDecklistParserService, DecklistParserService>();
            services.AddScoped<IDeckService, DeckService>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddIdentityServices(configuration);

            return services;
        }

        public IServiceCollection AddApiResponseCompression()
        {
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Clear();
                options.Providers.Add<BrotliCompressionProvider>();
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat([
                    "application/json",
                    "application/problem+json"
                ]);
            });

            services.Configure<BrotliCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Fastest;
            });

            return services;
        }

        public IServiceCollection AddForwardedHeadersSupport()
        {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

            return services;
        }

        public IServiceCollection AddJwtAuthentication(IConfiguration configuration, IHostEnvironment environment)
        {
            var jwtSettings = configuration.GetSection("JWT").Get<JwtSettings>()!;
            if (environment.IsEnvironment("Testing"))
            {
                return services;
            }

            var key = Encoding.UTF8.GetBytes(jwtSettings.SecretKey);

            services.AddAuthentication(options =>
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

            return services;
        }
    }
}
