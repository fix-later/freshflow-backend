using FreshFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StackExchange.Redis;
using Testcontainers.PostgreSql;

namespace FreshFlow.IntegrationTests.Infrastructure;

public sealed class AuthWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // ── Replace real AppDbContext with Testcontainers PostgreSQL ──────
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(opts =>
                opts.UseNpgsql(_postgres.GetConnectionString()));

            // ── Replace real IConnectionMultiplexer with no-op mock ───────────
            // The real singleton connects to localhost:6379 which is unavailable
            // in CI/test environments. With abortConnect=false it connects but
            // every Redis command blocks for syncTimeout (~5 s) before throwing.
            // Swap it out so Redis calls return immediately without error.
            var redisDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IConnectionMultiplexer));
            if (redisDescriptor is not null) services.Remove(redisDescriptor);

            var mockDb = Substitute.For<IDatabase>();
            var mockMultiplexer = Substitute.For<IConnectionMultiplexer>();
            mockMultiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(mockDb);
            services.AddSingleton(mockMultiplexer);
        });

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminSeed:Email"] = "admin@test.freshflow",
                ["AdminSeed:Password"] = "AdminP@ss1",
                ["JWT:Key"] = "integration-test-secret-key-min-32-chars!!",
                ["JWT:Issuer"] = "https://test.freshflow",
                ["JWT:Audience"] = "freshflow-api",
                // Raise the auth rate limit so individual integration test classes don't
                // accidentally exhaust the 10-request production cap across their tests.
                ["RateLimiting:Auth:PermitLimit"] = "1000",
                // Provide a dummy Redis connection string so startup validation passes
                // (the real multiplexer is replaced above, so this string is never used).
                ["ConnectionStrings:Redis"] = "localhost:6379,abortConnect=false"
            });
        });
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
