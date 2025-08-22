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

        public CustomWebApplicationFactory()
        {
            // Create unique database names for THIS factory instance
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssffff");
            var random = Guid.NewGuid().ToString("N")[..8];
            var threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            
            _testMainDbName = $"test_main_{timestamp}_{threadId}_{random}";
            _testIdentityDbName = $"test_identity_{timestamp}_{threadId}_{random}";
        }

        public async Task InitializeAsync()
        {
            // Create test databases for this instance
            await CreateTestDatabases();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, configBuilder) =>
            {
                // Add test configuration
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Use THIS instance's unique database names
                    ["ConnectionStrings:DefaultConnection"] = $"Server=postgres; Port=5433;Uid=root; Pwd=supersecretlongpassword; Database={_testMainDbName}",
                    ["ConnectionStrings:IdentityConnection"] = $"Server=postgres; Port=5433;Uid=root; Pwd=supersecretlongpassword; Database={_testIdentityDbName}",
                    ["ConnectionStrings:Redis"] = "", // Disable Redis for tests

                    // JWT settings from your appsettings
                    ["JWT:SecretKey"] = "A6e48opuUdbWcyLLHIwbeKVo826sEYYgepR9XtedHfRqhJ5drn7Nl85Yvb6WHsaYmp22gOXAsCOQvFzbxxqI3FQ2DUuf3C07Z50EaINValFi4FuuPX1pptcBHrzvl4qp",
                    ["JWT:Issuer"] = "MTG25API",
                    ["JWT:Audience"] = "MTG25Users",
                    ["JWT:ExpiryMinutes"] = "60",

                    // Other settings
                    ["Scryfall:BasePath"] = "https://api.scryfall.com/",
                    ["Paths:Bulk"] = "/tmp/test-bulk-data"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Remove existing DbContexts if they exist
                services.RemoveAll(typeof(DbContextOptions<MainContext>));
                services.RemoveAll(typeof(DbContextOptions<AppIdentityDbContext>));
                services.RemoveAll<MainContext>();
                services.RemoveAll<AppIdentityDbContext>();

                // Add test database contexts with unique names
                services.AddDbContext<MainContext>(options =>
                    options.UseNpgsql($"Server=postgres; Port=5433;Uid=root; Pwd=supersecretlongpassword; Database={_testMainDbName}"));

                services.AddDbContext<AppIdentityDbContext>(options =>
                    options.UseNpgsql($"Server=postgres; Port=5433;Uid=root; Pwd=supersecretlongpassword; Database={_testIdentityDbName}"));

                // Replace Redis with in-memory cache
                services.RemoveAll(typeof(IDistributedCache));
                services.AddMemoryCache();
                services.AddSingleton<IDistributedCache, MemoryDistributedCache>();

                // Remove existing authentication services
                services.RemoveAll<IAuthenticationSchemeProvider>();

                // Add test authentication
                services.AddAuthentication("Test")
                    .AddScheme<TestAuthenticationSchemeOptions, TestAuthenticationHandler>("Test", options => { });

                services.Configure<AuthorizationOptions>(options =>
                {
                    options.DefaultPolicy = new AuthorizationPolicyBuilder("Test")
                        .RequireAuthenticatedUser()
                        .Build();
                });
            });
        }

        private async Task CreateTestDatabases()
        {
            // Connect to postgres database to create test databases
            var connectionString = "Server=postgres; Port=5433;Uid=root; Pwd=supersecretlongpassword; Database=postgres";
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Create main test database
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"CREATE DATABASE \"{_testMainDbName}\"";
                await command.ExecuteNonQueryAsync();
            }

            // Create identity test database
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"CREATE DATABASE \"{_testIdentityDbName}\"";
                await command.ExecuteNonQueryAsync();
            }

            // Run migrations
            await RunMigrations();
        }

        private async Task RunMigrations()
        {
            // Run migrations on main database
            var mainConnectionString = $"Server=postgres; Port=5433;Uid=root; Pwd=supersecretlongpassword; Database={_testMainDbName}";
            using (var context = new MainContext(new DbContextOptionsBuilder<MainContext>()
                .UseNpgsql(mainConnectionString).Options))
            {
                await context.Database.MigrateAsync();
            }

            // Run migrations on identity database
            var identityConnectionString = $"Server=postgres; Port=5433;Uid=root; Pwd=supersecretlongpassword; Database={_testIdentityDbName}";
            using (var context = new AppIdentityDbContext(new DbContextOptionsBuilder<AppIdentityDbContext>()
                .UseNpgsql(identityConnectionString).Options))
            {
                await context.Database.MigrateAsync();
            }
        }

        public override async ValueTask DisposeAsync()
        {
            // Clean up test databases first
            await DropTestDatabases();

            // Then call base cleanup
            await base.DisposeAsync();
        }

        private async Task DropTestDatabases()
        {
            try
            {
                var connectionString = "Server=postgres; Port=5433;Uid=root; Pwd=supersecretlongpassword; Database=postgres";
                using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                // Terminate active connections
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"
                        SELECT pg_terminate_backend(pid)
                        FROM pg_stat_activity
                        WHERE datname IN ('{_testMainDbName}', '{_testIdentityDbName}')
                        AND pid <> pg_backend_pid()";
                    await command.ExecuteNonQueryAsync();
                }

                // Drop databases
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"DROP DATABASE IF EXISTS \"{_testMainDbName}\"";
                    await command.ExecuteNonQueryAsync();
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"DROP DATABASE IF EXISTS \"{_testIdentityDbName}\"";
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to clean up test databases: {ex.Message}");
            }
        }

        // FIXED: Properly configure authentication with user details
        public HttpClient CreateClientWithUser(string userId, string userName = "testuser", string email = "test@example.com")
        {
            var client = CreateClient();
            
            // Set custom headers that the test authentication handler can read
            client.DefaultRequestHeaders.Add("Test-UserId", userId);
            client.DefaultRequestHeaders.Add("Test-UserName", userName);
            client.DefaultRequestHeaders.Add("Test-Email", email);
            
            return client;
        }
    }
}
