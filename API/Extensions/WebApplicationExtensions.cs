using System.Text.Json;
using API.Endpoints.Accounts;
using API.Endpoints.Binders;
using API.Endpoints.Cards;
using API.Endpoints.Collections;
using API.Endpoints.Decks;
using API.Endpoints.Friends;
using API.Endpoints.Leagues;
using API.Endpoints.Notifications;
using API.Endpoints.Trades;
using API.Endpoints.Wishlists;
using API.Helpers;
using API.Services;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

namespace API.Extensions;

public static class WebApplicationExtensions
{
    extension(WebApplication app)
    {
        public async Task ApplyMigrationsAsync()
        {
            using var scope = app.Services.CreateScope();
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

        public async Task LoadCardDataIfNeededAsync()
        {
            if (app.Environment.IsEnvironment("Testing"))
            {
                return;
            }

            var cardDataService = app.Services.GetRequiredService<CardDataService>();
            await cardDataService.LoadCardDataAsync();
        }

        public void UseApiSwagger()
        {
            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Integration") || app.Configuration.GetValue<bool>("Swagger:Enabled"))
            {
                app.UseDeveloperExceptionPage();
                app.MapOpenApi();
                app.MapScalarApiReference();
                app.UseSwagger();
                app.UseSwaggerUI();
            }
        }

        public void UseApiForwardedHeaders()
        {
            app.UseForwardedHeaders();
        }

        public void UseApiSecurityHeaders()
        {
            if (app.Environment.IsEnvironment("Integration") || app.Environment.IsProduction())
            {
                app.UseHsts();
                app.UseHttpsRedirection();
            }
        }

        public void UseApiExceptionHandling()
        {
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
        }

        public void UseApiNotFoundHandler()
        {
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
        }

        public void MapApiEndpoints()
        {
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

            app.MapGet("/api/health", () => Results.Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                environment = app.Environment.EnvironmentName,
                version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            })).AllowAnonymous();
        }
    }
}
