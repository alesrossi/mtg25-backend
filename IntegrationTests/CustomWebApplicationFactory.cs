using Core.Models.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Infrastructure.Data;
using TestUtilities.Authentication;
using API;
using Microsoft.AspNetCore.Authentication;
using Npgsql;

namespace IntegrationTests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _testMainDbName;
        private readonly string _testDefaultConnection;
        private readonly string _adminConnection;

        public CustomWebApplicationFactory()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssffff");
            var random    = Guid.NewGuid().ToString("N")[..8];
            var threadId  = Thread.CurrentThread.ManagedThreadId;

            _testMainDbName = $"test_main_{timestamp}_{threadId}_{random}";

            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            var baseConfig = new ConfigurationBuilder()
                                .SetBasePath(AppContext.BaseDirectory)
                                .AddJsonFile("appsettings.json",        optional: false)
                                .AddJsonFile($"appsettings.{env}.json", optional: true)
                                .AddEnvironmentVariables()
                                .Build();

            var defaultConn = baseConfig.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection missing");

            _testDefaultConnection = new NpgsqlConnectionStringBuilder(defaultConn) { Database = _testMainDbName }.ConnectionString;
            _adminConnection       = new NpgsqlConnectionStringBuilder(defaultConn) { Database = "postgres" }.ConnectionString;
        }

        public async Task InitializeAsync() => await CreateTestDatabase();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _testDefaultConnection,
                    ["ConnectionStrings:Redis"]             = ""
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<MainContext>));
                services.RemoveAll<MainContext>();

                services.AddDbContext<MainContext>(o => o.UseNpgsql(_testDefaultConnection));

                services.RemoveAll(typeof(IDistributedCache));
                services.AddMemoryCache();
                services.AddSingleton<IDistributedCache, MemoryDistributedCache>();

                services.RemoveAll<IAuthenticationSchemeProvider>();
                services.AddAuthentication("Test")
                        .AddScheme<TestAuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });

                services.Configure<AuthorizationOptions>(o =>
                {
                    o.DefaultPolicy = new AuthorizationPolicyBuilder("Test")
                                        .RequireAuthenticatedUser()
                                        .Build();
                    o.AddPolicy("OptionalAuth", policy =>
                    {
                        policy.AddAuthenticationSchemes("Test");
                        policy.RequireAssertion(_ => true);
                    });
                });
            });
        }

        private async Task CreateTestDatabase()
        {
            await using var conn = new NpgsqlConnection(_adminConnection);
            await conn.OpenAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"CREATE DATABASE \"{_testMainDbName}\"";
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var ctx = new MainContext(new DbContextOptionsBuilder<MainContext>()
                       .UseNpgsql(_testDefaultConnection).Options))
            {
                await ctx.Database.MigrateAsync();
            }
        }

        public override async ValueTask DisposeAsync()
        {
            await DropTestDatabase();
            await base.DisposeAsync();
        }

        private async Task DropTestDatabase()
        {
            try
            {
                NpgsqlConnection.ClearAllPools();
                await Task.Delay(100);

                await using var conn = new NpgsqlConnection(_adminConnection);
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $@"
                        SELECT pg_terminate_backend(pid)
                        FROM pg_stat_activity
                        WHERE datname = '{_testMainDbName}'
                          AND pid <> pg_backend_pid()";
                await cmd.ExecuteNonQueryAsync();

                await Task.Delay(50);

                cmd.CommandText = $"DROP DATABASE IF EXISTS \"{_testMainDbName}\"";
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($@"Warning: Failed to clean up test database {_testMainDbName}: {ex.Message}");
            }
        }

        public HttpClient CreateClientWithUser(string userId,
                                               string userName = "testuser",
                                               string email    = "test@example.com")
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Add("Test-UserId",  userId);
            client.DefaultRequestHeaders.Add("Test-UserName", userName);
            client.DefaultRequestHeaders.Add("Test-Email",   email);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
            return client;
        }
    }
}
