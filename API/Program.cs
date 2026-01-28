using API.Extensions;
using API.Logging;
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

            builder.Services.AddAuthorization();
            builder.Services.AddJwtAuthentication(builder.Configuration, builder.Environment);
        
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
            app.UseCors("DefaultCors");

            app.MapApiEndpoints();
        
        
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
