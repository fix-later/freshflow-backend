using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Pricing;

/// <summary>
/// FR-PRI-004 Acceptance Test — UC-PRI-06 Save Price History.
///
/// Verifies that every price OR quantity update appends a distinct, immutable
/// <see cref="PriceSnapshot"/> row retrievable in descending <c>RecordedAt</c> order.
///
/// Read-side join note:
///   <c>price_snapshots.market_product_id → market_products.id → market_products.market_id</c>
///   UC-PRI-10 (View Price Change Detail) resolves <c>marketId</c> via this join through
///   <see cref="IPriceSnapshotRepository.GetByMarketProductIdAsync"/> after the caller
///   supplies the <c>marketProductId</c> (resolved from <c>marketId + productId</c>).
///   No denormalized <c>market_id</c> column on <c>price_snapshots</c> is needed.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PriceHistoryAcceptanceTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private const int UpdateCount = 12; // > 10 per FR-PRI-004 #4

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task MultipleUpdates_CreateDistinctOrderedSnapshotsAsync()
    {
        // Capture time BEFORE any arrange/act work so the assertion in #4e uses a stable
        // lower bound instead of re-evaluating UtcNow at assertion time (which would be
        // flaky on slow CI where the test takes > 1 minute to run).
        var testStartedAt = DateTime.UtcNow;

        // ── Arrange: market, agent, product, market_product ───────────────────
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-PH-{Guid.NewGuid():N}");
        var agentEmail = $"agent-ph-{Guid.NewGuid():N}@test.freshflow";
        await CreateMarketAgentAsync(agentEmail, marketId);

        var unitId = await GetOrCreateUnitAsync("kg");
        var productId = await CreateProductAsync($"Cá hồi {Guid.NewGuid():N}", unitId);
        var marketProductId = await SeedMarketProductAsync(marketId, productId, 100_000m, 500);

        var agentToken = await LoginAsync(agentEmail, "AgentP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentToken);

        // ── Act: alternate price and quantity updates ──────────────────────────
        for (var i = 0; i < UpdateCount; i++)
        {
            if (i % 2 == 0)
            {
                // Price update (PATCH /price)
                var newPrice = 100_000m + (i * 1_000m);
                var resp = await _client.PatchAsJsonAsync(
                    $"/api/v1/markets/{marketId}/products/{productId}/price",
                    new { price = newPrice });
                resp.EnsureSuccessStatusCode();
            }
            else
            {
                // Quantity update (PATCH /quantity)
                var newQty = 500 + i;
                var resp = await _client.PatchAsJsonAsync(
                    $"/api/v1/markets/{marketId}/products/{productId}/quantity",
                    new { quantity = newQty });
                resp.EnsureSuccessStatusCode();
            }
        }

        // ── Assert: snapshots persisted correctly ─────────────────────────────
        using var scope = factory.Services.CreateScope();
        var snapshotRepo = scope.ServiceProvider.GetRequiredService<IPriceSnapshotRepository>();

        var snapshots = await snapshotRepo.GetByMarketProductIdAsync(
            marketProductId, CancellationToken.None);

        // FR-PRI-004 #4a: at least N snapshots created
        snapshots.Count.Should().BeGreaterThanOrEqualTo(UpdateCount,
            "every price or quantity update must produce an immutable snapshot row");

        // FR-PRI-004 #4b: all belong to the correct market_product
        snapshots.Should().OnlyContain(s => s.MarketProductId == marketProductId);

        // FR-PRI-004 #4c: sorted descending by RecordedAt (repository contract)
        snapshots.Should().BeInDescendingOrder(s => s.RecordedAt,
            "GetByMarketProductIdAsync must return snapshots newest-first");

        // FR-PRI-004 #4d: all RecordedAt values are distinct (each update is a separate event)
        var distinctTimestamps = snapshots.Select(s => s.RecordedAt).Distinct().Count();
        distinctTimestamps.Should().Be(snapshots.Count,
            "each snapshot must have a unique RecordedAt timestamp");

        // FR-PRI-004 #4e: snapshots were created after this test started.
        // Using the pre-captured testStartedAt (not UtcNow.AddMinutes(-1)) avoids
        // a race on slow CI where the test itself takes longer than 1 minute.
        snapshots.Should().OnlyContain(s =>
            s.RecordedAt >= testStartedAt,
            "all snapshots must have been recorded during this test run");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> LoginAsync(string identifier, string password)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier, password });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        return env!.Data!.AccessToken;
    }

    private async Task<Guid> CreateMarketAsync(string name)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/markets", new
        {
            name,
            location = "Test Zone",
            address = "1 Test St",
            latitude = (decimal?)null,
            longitude = (decimal?)null
        });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<IdBody>>();
        return env!.Data!.Id;
    }

    private async Task CreateMarketAgentAsync(string email, Guid marketId)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password = "AgentP@ss1",
            role = "market_agent",
            marketId
        });
        resp.EnsureSuccessStatusCode();
    }

    private async Task<Guid> GetOrCreateUnitAsync(string unitName)
    {
        var listResp = await _client.GetAsync("/api/v1/units");
        listResp.EnsureSuccessStatusCode();
        var listEnv = await listResp.Content.ReadFromJsonAsync<Envelope<List<UnitBody>>>();
        var existing = listEnv!.Data?.FirstOrDefault(u =>
            u.Name.Equals(unitName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing.Id;

        var resp = await _client.PostAsJsonAsync("/api/v1/units", new { name = unitName });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<IdBody>>();
        return env!.Data!.Id;
    }

    private async Task<Guid> CreateProductAsync(string name, Guid unitId)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            name,
            unitId,
            categoryId = (Guid?)null,
            description = (string?)null
        });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<IdBody>>();
        return env!.Data!.Id;
    }

    /// <summary>Seeds a MarketProduct and returns its <c>Id</c> for snapshot queries.</summary>
    private async Task<Guid> SeedMarketProductAsync(
        Guid marketId, Guid productId, decimal price, int quantity)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mp = new MarketProduct(marketId, productId, price, quantity, null);
        await db.Set<MarketProduct>().AddAsync(mp);
        await db.SaveChangesAsync();
        return mp.Id;
    }
}
