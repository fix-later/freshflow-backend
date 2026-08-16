using FreshFlow.API.Assistant.Abstractions;
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

/// <summary>
/// WebApplicationFactory for the AI assistant chat endpoint (SCRUM-251/252/253). Mirrors
/// <see cref="AuthWebAppFactory"/> (Testcontainers Postgres + no-op Redis + JWT/admin-seed config) and
/// additionally swaps the real <see cref="IAssistantChatClient"/> for a scripted fake so tests exercise
/// the full HTTP → controller → orchestrator → tool/gate → conversation-store path without calling GLM.
/// <see cref="AssistantPermitLimit"/> is overridable so the rate-limit test can use a tiny budget.
/// </summary>
public class AssistantWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    /// <summary>The scripted LLM the tests control; resolved as the app's <see cref="IAssistantChatClient"/>.</summary>
    public ScriptedAssistantChatClient ChatClient { get; } = new();

    /// <summary>Per-window request budget for the "assistant" rate-limit policy (high by default).</summary>
    protected virtual int AssistantPermitLimit => 100;

    public async Task InitializeAsync() => await _postgres.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor is not null) services.Remove(dbDescriptor);
            services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(_postgres.GetConnectionString()));

            var redisDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IConnectionMultiplexer));
            if (redisDescriptor is not null) services.Remove(redisDescriptor);
            var mockDb = Substitute.For<IDatabase>();
            var mockMultiplexer = Substitute.For<IConnectionMultiplexer>();
            mockMultiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(mockDb);
            services.AddSingleton(mockMultiplexer);

            // Replace the real ZenMux-backed chat client with the scripted fake — no GLM calls in CI.
            var chatDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAssistantChatClient));
            if (chatDescriptor is not null) services.Remove(chatDescriptor);
            services.AddSingleton<IAssistantChatClient>(ChatClient);
        });

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminSeed:Email"] = "admin@assistant.test",
                ["AdminSeed:Password"] = "AdminP@ss1",
                ["JWT:Key"] = "assistant-integration-test-secret-min-32-chars!!",
                ["JWT:Issuer"] = "https://test.freshflow",
                ["JWT:Audience"] = "freshflow-api",
                ["Cloudinary:CloudName"] = "assistant-integration-test-cloud",
                ["Cloudinary:ApiKey"] = "assistant-integration-test-key",
                ["Cloudinary:ApiSecret"] = "assistant-integration-test-secret",
                ["RateLimiting:Auth:PermitLimit"] = "1000",
                ["RateLimiting:Assistant:PermitLimit"] = AssistantPermitLimit.ToString(),
                ["ConnectionStrings:Redis"] = "localhost:6379,abortConnect=false",
                // Non-empty so ZenMuxOptions ValidateOnStart passes; the real client is never built.
                ["Assistant:ZenMux:ApiKey"] = "test-key-not-used"
            });
        });
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
