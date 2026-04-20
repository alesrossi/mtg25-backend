using API.Extensions;
using API.Logging;
using API.Services;
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
                SerilogConfigurator.EnableSerilogSelfLog(context.Configuration);
                loggerConfiguration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext();

                SerilogConfigurator.ConfigureElasticsearchLogging(context.Configuration, loggerConfiguration);
            });

            builder.Services
                .AddApiConfiguration(builder.Configuration)
                .AddDatabaseContexts(builder.Configuration)
                .AddCaching(builder.Configuration)
                .AddApiSwagger()
                .AddApiCors(builder.Configuration)
                .AddApiServices(builder.Configuration)
                .AddApiResponseCompression()
                .AddForwardedHeadersSupport();

            builder.Services.AddAuthorization(options =>
            {
                // Policy that allows both authenticated and anonymous users
                // but still triggers authentication middleware
                options.AddPolicy("OptionalAuth", policy =>
                {
                    policy.AddAuthenticationSchemes("Bearer");
                    policy.RequireAssertion(_ => true);
                });
            });
            builder.Services.AddJwtAuthentication(builder.Configuration, builder.Environment);
            builder.Services.AddScoped<ITeamService, TeamService>();
            builder.Services.AddScoped<ITeamCollectionService, TeamCollectionService>();
        
            var app = builder.Build();
        
            await app.ApplyMigrationsAsync();
            await app.LoadCardDataIfNeededAsync();

            app.UseApiSwagger();
            app.UseApiForwardedHeaders();
            app.UseApiSecurityHeaders();
            app.UseApiExceptionHandling();

            app.UseRequestLogging();
            app.UseResponseCompression();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseApiNotFoundHandler();
            // TODO: discutere con responsabile - valutare spostare UseCors prima di UseAuthentication/UseAuthorization per gestire correttamente le preflight requests CORS
            app.UseCors("DefaultCors");

            app.MapApiEndpoints();
            app.MapTeamsEndpoints();
        
        
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

}
