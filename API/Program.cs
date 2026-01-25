using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.Configuration;
using API.Extensions;
using API.Filters;
using API.Helpers;
using API.Json;
using API.Services;
using Core.Interfaces;
using Core.Models.Identity;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.IO.Compression;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Elasticsearch.Net;
using API.Endpoints.Accounts;
using API.Endpoints.Binders;
using API.Endpoints.Cards;
using API.Endpoints.Collections;
using API.Endpoints.Decks;
using API.Endpoints.Leagues;
using API.Endpoints.Notifications;
using API.Endpoints.Friends;
using API.Endpoints.Trades;
using API.Endpoints.Wishlists;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;
using Serilog.Debugging;

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
                EnableSerilogSelfLog(context.Configuration);
                loggerConfiguration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext();

                ConfigureElasticsearchLogging(context.Configuration, loggerConfiguration);
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
                    options.JsonSerializerOptions.Converters.Add(new LanguageJsonConverter());
                    options.JsonSerializerOptions.Converters.Add(new NullableLanguageJsonConverter());
                });

            // Configure JSON for Minimal APIs
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                options.SerializerOptions.Converters.Add(new LanguageJsonConverter());
                options.SerializerOptions.Converters.Add(new NullableLanguageJsonConverter());
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
                var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
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
            builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
            
            builder.Services.AddSingleton<CardDataService>();
            builder.Services.AddScoped<ProblemDetailsEndpointFilter>();
            builder.Services.AddScoped<IJwtService, JwtService>();
            builder.Services.AddScoped<DeckCardService>();
            builder.Services.AddScoped<WishlistPricingService>();
            builder.Services.AddScoped<IUserSettingsService, UserSettingsService>();
            builder.Services.AddScoped<ICollectionService, CollectionService>();
            builder.Services.AddScoped<ITradeSessionStore, TradeSessionStore>();
            builder.Services.AddScoped<ITradeConnectionService, TradeConnectionService>();
            builder.Services.AddScoped<IFriendService, FriendService>();
            builder.Services.AddScoped<ILeagueService, LeagueService>();
            builder.Services.AddScoped<IWishlistService, WishlistService>();
            builder.Services.AddScoped<ICardsService, CardsService>();
            builder.Services.AddScoped<IBindersService, BindersService>();
            builder.Services.AddScoped<IMessageLocalizer, MessageLocalizationService>();
            builder.Services.AddScoped<NotificationService>();
            builder.Services.AddScoped<IDeckValidationService, DeckValidationService>();
            builder.Services.AddScoped<IDecklistParserService, DecklistParserService>();
            builder.Services.AddScoped<IDeckService, DeckService>();
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

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });
            
            // JWT Configuration
            
            // Configure Authentication
            var jwtSettings = builder.Configuration.GetSection("JWT").Get<JwtSettings>()!;
            // Only configure JWT if not in testing environment and JWT config is available
            if (!builder.Environment.IsEnvironment("Testing"))
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
            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Integration") || app.Configuration.GetValue<bool>("Swagger:Enabled"))
            {
                app.UseDeveloperExceptionPage();
                app.MapOpenApi();
                app.MapScalarApiReference();
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseForwardedHeaders();

            if (app.Environment.IsEnvironment("Integration") || app.Environment.IsProduction())
            {
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
                    var exception = exceptionFeature?.Error;

                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(exception, "Unhandled exception encountered while processing the request");

                    var statusCode = exception switch
                    {
                        BadHttpRequestException badRequestException => badRequestException.StatusCode,
                        JsonException => StatusCodes.Status400BadRequest,
                        _ => StatusCodes.Status500InternalServerError
                    };
                    var problemDetails = new ProblemDetails
                    {
                        Status = statusCode,
                        Title = "An unexpected error occurred",
                        Detail = app.Environment.IsDevelopment() ? exception?.Message : "An unexpected error occurred while processing the request.",
                        Instance = context.Request.Path,
                        Type = $"https://httpstatuses.io/{statusCode}",
                        Extensions =
                        {
                            ["traceId"] = context.TraceIdentifier
                        }
                    };

                    context.Response.StatusCode = statusCode;
                    context.Response.ContentType = "application/problem+json";
                    await context.Response.WriteAsJsonAsync(problemDetails);
                });
            });
        
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

            app.UseCors("DefaultCors");
        
            app.MapCardsEndpoints();
            app.MapAccountEndpoints();
            app.MapCollectionsEndpoints();
            app.MapBindersEndpoints();
            app.MapWishlistsEndpoints();
            app.MapLeaguesEndpoints();
            app.MapDecksEndpoints();
            app.MapTradeEndpoints();
            app.MapFriendEndpoints();
            app.MapNotificationsEndpoints();
        
            // Add health check endpoint
            app.MapGet("/api/health", () => Results.Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                environment = app.Environment.EnvironmentName,
                version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            })).AllowAnonymous();
        
        
            await app.RunAsync();
        }
        catch (HostAbortedException)
        {
            Log.Information("MTG25 host aborted by tooling request");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unhandled exception during MTG25 host execution");
            throw;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static void ConfigureElasticsearchLogging(IConfiguration configuration, LoggerConfiguration loggerConfiguration)
    {
        var section = configuration.GetSection("ElasticsearchLogging");
        var nodeUris = section["NodeUris"];
        if (string.IsNullOrWhiteSpace(nodeUris))
        {
            Console.Error.WriteLine("Elasticsearch logging disabled: missing ElasticsearchLogging:NodeUris");
            return;
        }

        var sinkOptions = new ElasticsearchSinkOptions(new Uri(nodeUris))
        {
            IndexFormat = section["IndexFormat"],
            AutoRegisterTemplate = section.GetValue("AutoRegisterTemplate", true),
            EmitEventFailure = ParseEmitEventFailure(section["EmitEventFailure"]) | EmitEventFailureHandling.RaiseCallback
        };
        sinkOptions.FailureCallback = (logEvent, exception) =>
        {
            var rendered = logEvent?.RenderMessage() ?? "<null>";
            var error = exception?.Message ?? "<no exception>";
            Console.Error.WriteLine($"Elasticsearch sink failure: {rendered} | {error}");
        };

        var templateVersion = section["AutoRegisterTemplateVersion"];
        if (!string.IsNullOrWhiteSpace(templateVersion) &&
            Enum.TryParse(templateVersion, ignoreCase: true, out AutoRegisterTemplateVersion parsedVersion))
        {
            sinkOptions.AutoRegisterTemplateVersion = parsedVersion;
        }

        var username = section["Username"];
        var password = section["Password"];
        Console.Error.WriteLine($"Elasticsearch logging enabled: nodeUris={nodeUris}, auth={(string.IsNullOrWhiteSpace(username) ? "none" : "basic")}");
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            sinkOptions.ModifyConnectionSettings = connection =>
                connection.BasicAuthentication(username, password);
        }

        var caPath = section["CaCertificatePath"];
        if (!string.IsNullOrWhiteSpace(caPath))
        {
            var existing = sinkOptions.ModifyConnectionSettings;
            sinkOptions.ModifyConnectionSettings = connection =>
            {
                if (existing != null)
                {
                    connection = existing(connection);
                }

                return connection.ServerCertificateValidationCallback(
                    (sender, certificate, chain, errors) => ValidateElasticCertificate(certificate, caPath, errors));
            };
            Console.Error.WriteLine($"Elasticsearch logging CA path: {caPath} exists={File.Exists(caPath)}");
        }

        loggerConfiguration.WriteTo.Elasticsearch(sinkOptions);
    }


    private static void EnableSerilogSelfLog(IConfiguration configuration)
    {
        var enabled = configuration.GetValue("SerilogSelfLog:Enabled", false);
        if (!enabled)
        {
            return;
        }

        SelfLog.Enable(message => Console.Error.WriteLine($"SerilogSelfLog: {message}"));
    }

    private static EmitEventFailureHandling ParseEmitEventFailure(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return EmitEventFailureHandling.WriteToSelfLog;
        }

        return Enum.TryParse(value, ignoreCase: true, out EmitEventFailureHandling parsed)
            ? parsed
            : EmitEventFailureHandling.WriteToSelfLog;
    }

    private static bool ValidateElasticCertificate(X509Certificate? certificate, string caPath, SslPolicyErrors errors)
    {
        if (certificate == null || !File.Exists(caPath))
        {
            return false;
        }

        if (errors == SslPolicyErrors.None)
        {
            return true;
        }

        var caCertificate = new X509Certificate2(caPath);
        var serverCertificate = certificate as X509Certificate2 ?? new X509Certificate2(certificate);

        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        chain.ChainPolicy.ExtraStore.Add(caCertificate);

        if (!chain.Build(serverCertificate))
        {
            return false;
        }

        var root = chain.ChainElements[^1].Certificate;
        return string.Equals(root.Thumbprint, caCertificate.Thumbprint, StringComparison.OrdinalIgnoreCase);
    }
}
