using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace FreshFlow.IntegrationTests.Auth;

/// <summary>
/// Verifies that the "auth" rate-limit policy is enforced on AuthController and that
/// throttled responses return the standard API error envelope.
/// Uses a dedicated factory (PermitLimit = 3) so only 4 requests are needed.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RateLimitingTests(RateLimitingTests.RateLimitFactory factory)
    : IClassFixture<RateLimitingTests.RateLimitFactory>
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Standalone factory that mirrors AuthWebAppFactory but sets PermitLimit = 3
    /// so the 4th request triggers 429 without needing 11 requests.
    /// </summary>
    public sealed class RateLimitFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres =
            new PostgreSqlBuilder("postgres:16-alpine").Build();

        public async Task InitializeAsync() => await _postgres.StartAsync();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor is not null) services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(opts =>
                    opts.UseNpgsql(_postgres.GetConnectionString()));
            });

            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AdminSeed:Email"] = "admin@ratelimit.test",
                    ["AdminSeed:Password"] = "AdminP@ss1",
                    ["JWT:Key"] = "rate-limit-test-secret-key-min-32-chars!!",
                    ["JWT:Issuer"] = "https://test.freshflow",
                    ["JWT:Audience"] = "freshflow-api",
                    ["Cloudinary:CloudName"] = "rate-limit-test-cloud",
                    ["Cloudinary:ApiKey"] = "rate-limit-test-key",
                    ["Cloudinary:ApiSecret"] = "rate-limit-test-secret",
                    // Low limit so the 4th request triggers 429 — no BCrypt overhead needed.
                    ["RateLimiting:Auth:PermitLimit"] = "3"
                }));
        }

        public new async Task DisposeAsync()
        {
            await _postgres.DisposeAsync();
            await base.DisposeAsync();
        }
    }

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task AuthEndpoint_AfterPermitLimitReached_Returns429WithErrorEnvelope()
    {
        // Use forgot-password: unknown email → 202 Accepted immediately (no BCrypt cost).
        const string path = "/api/v1/auth/forgot-password";
        var body = new { identifier = "rl.canary@nobody.invalid" };

        // First 3 requests must be accepted (permit limit = 3)
        for (var i = 1; i <= 3; i++)
        {
            var resp = await _client.PostAsJsonAsync(path, body);
            resp.StatusCode.Should().Be(HttpStatusCode.Accepted,
                because: $"request #{i} should be within the allowed window");
        }

        // The 4th request must be blocked by the rate limiter
        var blocked = await _client.PostAsJsonAsync(path, body);

        // Assert status code
        blocked.StatusCode.Should().Be(
            HttpStatusCode.TooManyRequests,
            because: "the rate limiter should return 429 once the permit limit is exceeded");

        // Assert standard API error envelope shape (FU1)
        var envelope = await blocked.Content.ReadFromJsonAsync<ErrorEnvelope>(JsonOpts);
        envelope.Should().NotBeNull();
        envelope!.Success.Should().BeFalse(
            because: "a rate-limit error envelope must have success = false");
        envelope.Error.Should().NotBeNull();
        envelope.Error!.Code.Should().Be("TOO_MANY_REQUESTS",
            because: "the rate limiter OnRejected callback sets this error code");
    }
}
