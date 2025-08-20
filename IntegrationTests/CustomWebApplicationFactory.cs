using Core.Models.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Infrastructure.Data;
using Infrastructure.Identity;
using TestUtilities.Authentication;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using API;
using DotNet.Testcontainers.Configurations;

namespace IntegrationTests
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private static PostgreSqlContainer? _dbContainer;
        private static RedisContainer? _redisContainer;
        private static bool _initialized = false;
        private static readonly object _lock = new object();

        public async Task InitializeAsync()
        {
            // Disable resource reaper to avoid initialization issues
            TestcontainersSettings.ResourceReaperEnabled = false;
            
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                // Create PostgreSQL container
                _dbContainer = new PostgreSqlBuilder()
                    .WithImage("postgres:15")
                    .WithDatabase("mtg25_test_main")
                    .WithUsername("test_user")
                    .WithPassword("test_password")
                    .WithEnvironment("POSTGRES_HOST_AUTH_METHOD", "trust")
                    .WithCommand("-c", "listen_addresses=*")
                    .WithAutoRemove(true)
                    .WithCleanUp(true)
                    .Build();

                // Create Redis container
                _redisContainer = new RedisBuilder()
                    .WithImage("redis:7-alpine")
                    .WithAutoRemove(true)
                    .WithCleanUp(true)
                    .Build();

                _initialized = true;
            }

            // Start containers sequentially to avoid conflicts
            await _dbContainer.StartAsync();
            await _redisContainer.StartAsync();
            
            // Give containers time to fully start
            await Task.Delay(10000);
            
            // Create identity database
            await CreateIdentityDatabase();
        }

        public new async Task DisposeAsync()
        {
            if (_dbContainer != null)
                await _dbContainer.DisposeAsync();
            if (_redisContainer != null)
                await _redisContainer.DisposeAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove existing contexts
                services.RemoveAll(typeof(DbContextOptions<MainContext>));
                services.RemoveAll(typeof(MainContext));
                services.RemoveAll(typeof(DbContextOptions<AppIdentityDbContext>));
                services.RemoveAll(typeof(AppIdentityDbContext));
                services.RemoveAll(typeof(IDistributedCache));

                // Configure connection strings
                var mainConnectionString = _dbContainer!.GetConnectionString() + ";CommandTimeout=30;";
                var identityConnectionString = mainConnectionString.Replace("mtg25_test_main", "mtg25_test_identity");

                // Register DbContexts
                services.AddDbContext<MainContext>(options =>
                    options.UseNpgsql(mainConnectionString));

                services.AddDbContext<AppIdentityDbContext>(options =>
                    options.UseNpgsql(identityConnectionString));

                // Configure Redis cache
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = _redisContainer!.GetConnectionString();
                    options.InstanceName = "MTG25_Test";
                });

                // Configure test authentication
                services.AddAuthentication("Test")
                    .AddScheme<TestAuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });

                services.Configure<AuthorizationOptions>(options =>
                {
                    options.DefaultPolicy = new AuthorizationPolicyBuilder("Test")
                        .RequireAuthenticatedUser()
                        .Build();
                });
            });

            builder.UseEnvironment("Testing");
        }

        private async Task CreateIdentityDatabase()
        {
            var adminConnectionString = _dbContainer!.GetConnectionString()
                .Replace("mtg25_test_main", "postgres") + ";CommandTimeout=30;";

            const int maxRetries = 5;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var connection = new Npgsql.NpgsqlConnection(adminConnectionString);
                    await connection.OpenAsync();

                    using var command = connection.CreateCommand();
                    command.CommandText = "CREATE DATABASE mtg25_test_identity;";
                    await command.ExecuteNonQueryAsync();

                    Console.WriteLine("Identity database created successfully");
                    return;
                }
                catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P04")
                {
                    Console.WriteLine("Identity database already exists");
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Attempt {attempt} failed: {ex.Message}");
                    if (attempt == maxRetries)
                        throw;
                    await Task.Delay(1000 * attempt);
                }
            }
        }

        public HttpClient CreateClientWithUser(string userId, string userName = null, string email = null)
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
            client.DefaultRequestHeaders.Add("X-Test-UserName", userName ?? "testuser");
            client.DefaultRequestHeaders.Add("X-Test-Email", email ?? "test@example.com");
            return client;
        }
    }
}
