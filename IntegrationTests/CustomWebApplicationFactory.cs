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
using Infrastructure.Identity;
using TestUtilities.Authentication;
using API;
using Microsoft.AspNetCore.Authentication;
using Npgsql;

namespace IntegrationTests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _testMainDbName;
        private readonly string _testIdentityDbName;

        // NEW: generated test-run connection strings derived from the active appsettings file
        private readonly string _testDefaultConnection;
        private readonly string _testIdentityConnection;
        private readonly string _adminConnection;   // same host/port/user/pass but Database = postgres

        public CustomWebApplicationFactory()
        {
            // keep existing unique-name logic
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssffff");
            var random    = Guid.NewGuid().ToString("N")[..8];
            var threadId  = Thread.CurrentThread.ManagedThreadId;

            _testMainDbName     = $"test_main_{timestamp}_{threadId}_{random}";
            _testIdentityDbName = $"test_identity_{timestamp}_{threadId}_{random}";

            /* --------------------  minimal change starts here  -------------------- */
            // Pick the correct appsettings file based on ASPNETCORE_ENVIRONMENT
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

            var baseConfig = new ConfigurationBuilder()
                                .SetBasePath(AppContext.BaseDirectory)
                                .AddJsonFile("appsettings.json",           optional: false)
                                .AddJsonFile($"appsettings.{env}.json",    optional: true)
                                .AddEnvironmentVariables()
                                .Build();

            var defaultConn  = baseConfig.GetConnectionString("DefaultConnection")  ?? throw new InvalidOperationException("DefaultConnection missing");
            var identityConn = baseConfig.GetConnectionString("IdentityConnection") ?? throw new InvalidOperationException("IdentityConnection missing");

            // Swap only the Database part for this test run
            _testDefaultConnection  = new NpgsqlConnectionStringBuilder(defaultConn)  { Database = _testMainDbName     }.ConnectionString;
            _testIdentityConnection = new NpgsqlConnectionStringBuilder(identityConn) { Database = _testIdentityDbName }.ConnectionString;

            // Admin connection (same server/port/user/pass but DB = postgres)
            _adminConnection = new NpgsqlConnectionStringBuilder(defaultConn) { Database = "postgres" }.ConnectionString;
            /* --------------------  minimal change ends here    -------------------- */
        }

        public async Task InitializeAsync() => await CreateTestDatabases();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                // overwrite only the connection strings – everything else unchanged
                cfg.AddInMemoryCollection(new Dictionary<string,string?>
                {
                    ["ConnectionStrings:DefaultConnection"]  = _testDefaultConnection,
                    ["ConnectionStrings:IdentityConnection"] = _testIdentityConnection,
                    ["ConnectionStrings:Redis"]              = ""          // disable Redis cache
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<MainContext>));
                services.RemoveAll(typeof(DbContextOptions<AppIdentityDbContext>));
                services.RemoveAll<MainContext>();
                services.RemoveAll<AppIdentityDbContext>();

                services.AddDbContext<MainContext>(o => o.UseNpgsql(_testDefaultConnection));
                services.AddDbContext<AppIdentityDbContext>(o => o.UseNpgsql(_testIdentityConnection));

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
                    // Policy that allows both authenticated and anonymous users
                    // but still triggers authentication middleware
                    o.AddPolicy("OptionalAuth", policy =>
                    {
                        policy.AddAuthenticationSchemes("Test");
                        policy.RequireAssertion(_ => true);
                    });
                });
            });
        }

        /* ----------------------  unchanged logic below  ---------------------- */

        private async Task CreateTestDatabases()
        {
            await using var conn = new NpgsqlConnection(_adminConnection);
            await conn.OpenAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"CREATE DATABASE \"{_testMainDbName}\"";
                await cmd.ExecuteNonQueryAsync();

                cmd.CommandText = $"CREATE DATABASE \"{_testIdentityDbName}\"";
                await cmd.ExecuteNonQueryAsync();
            }

            await RunMigrations();
        }

        private async Task RunMigrations()
        {
            await using (var ctx = new MainContext(new DbContextOptionsBuilder<MainContext>()
                       .UseNpgsql(_testDefaultConnection).Options))
            {
                await ctx.Database.MigrateAsync();
            }

            await using (var ctx = new AppIdentityDbContext(new DbContextOptionsBuilder<AppIdentityDbContext>()
                       .UseNpgsql(_testIdentityConnection).Options))
            {
                await ctx.Database.MigrateAsync();
            }
        }

        public override async ValueTask DisposeAsync()
        {
            await DropTestDatabases();
            await base.DisposeAsync();
        }

        private async Task DropTestDatabases()
        {
            try
            {
                // Clear connection pools first to prevent "database is being accessed" errors
                NpgsqlConnection.ClearAllPools();
                
                // Give connections a moment to close
                await Task.Delay(100);

                await using var conn = new NpgsqlConnection(_adminConnection);
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                // Terminate all connections to the test databases
                cmd.CommandText = $@"
                        SELECT pg_terminate_backend(pid)
                        FROM pg_stat_activity
                        WHERE datname IN ('{_testMainDbName}', '{_testIdentityDbName}')
                          AND pid <> pg_backend_pid()";
                await cmd.ExecuteNonQueryAsync();
                    
                // Give terminated connections time to clean up
                await Task.Delay(50);

                // Drop databases
                cmd.CommandText = $"DROP DATABASE IF EXISTS \"{_testMainDbName}\"";
                await cmd.ExecuteNonQueryAsync();

                cmd.CommandText = $"DROP DATABASE IF EXISTS \"{_testIdentityDbName}\"";
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($@"Warning: Failed to clean up test databases {_testMainDbName}, {_testIdentityDbName}: {ex.Message}");
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
