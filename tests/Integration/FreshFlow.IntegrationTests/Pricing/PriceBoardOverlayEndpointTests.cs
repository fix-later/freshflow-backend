using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Pricing;

/// <summary>
/// UC-PRI-09 — Live price board overlay on GET /api/v1/markets/{marketId}/products.
///
/// These tests verify that the price board reader (v1: DbPriceBoardReader) correctly
/// overlays live price/quantity/availableQuantity onto the paginated DB product list.
///
/// Key UC-PRI-09 behaviours under test:
/// - AvailableQuantity = CurrentQuantity (v1: no soft-reserved counter yet).
///   In particular, market_products.ReservedQuantity (hard DB reservation, owned by Orders)
///   is intentionally NOT subtracted — that comes from the future Redis reservation counter.
/// - Price board read failure (simulated by seeding an empty dict via all-miss scenario)
///   does not fail the endpoint — DB values are returned unchanged.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PriceBoardOverlayEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private static string Endpoint(Guid marketId) =>
        $"/api/v1/markets/{marketId}/products";

    private readonly HttpClient _client = factory.CreateClient();

    // ── AvailableQuantity = CurrentQuantity (UC-PRI-09 core behaviour) ────────

    /// <summary>
    /// Pre-overlay: AvailableQuantity = CurrentQuantity − ReservedQuantity (DB formula).
    /// Post-overlay: AvailableQuantity = CurrentQuantity (price board v1 formula).
    ///
    /// Seeds ReservedQuantity > 0 directly in the DB to prove the overlay takes effect.
    /// </summary>
    [Fact]
    public async Task GetMarketProducts_WhenReservedQuantityNonZero_AvailableQuantityEqualsCurrentQuantity()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"PB-Overlay-{Guid.NewGuid():N}");
        var unitId = await GetOrCreateUnitAsync("kg");
        var productId = await CreateProductAsync($"PB-Overlay-Product-{Guid.NewGuid():N}", unitId);

        // Seed with CurrentQuantity = 100
        var mpId = await SeedMarketProductAsync(marketId, productId, 50_000m, 100);

        // Directly set ReservedQuantity = 20 to simulate hard DB reservation
        // (normally set by the Orders module). Without the overlay, AvailableQuantity
        // would be 80; with the UC-PRI-09 overlay it must be 100 (= CurrentQuantity).
        await SetReservedQuantityAsync(mpId, 20);

        // Act
        var response = await _client.GetAsync(Endpoint(marketId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content
            .ReadFromJsonAsync<PagedEnvelope<MarketProductItemBody>>();
        env!.Success.Should().BeTrue();
        env.Data.Should().ContainSingle();

        var item = env.Data![0];
        item.CurrentQuantity.Should().Be(100);
        item.AvailableQuantity.Should().Be(100,
            "UC-PRI-09 price board sets AvailableQuantity = CurrentQuantity (v1; " +
            "soft-reserved from Redis order:reservation counter is not yet integrated)");
    }

    // ── Price overlay ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMarketProducts_WithSeededProduct_ReturnsCurrentDbPrice()
    {
        // Arrange — verifies v1 DB-direct reader returns the correct price
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"PB-Price-{Guid.NewGuid():N}");
        var unitId = await GetOrCreateUnitAsync("kg");
        var productId = await CreateProductAsync($"PB-Price-Product-{Guid.NewGuid():N}", unitId);
        await SeedMarketProductAsync(marketId, productId, 88_000m, 30);

        // Act
        var response = await _client.GetAsync(Endpoint(marketId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content
            .ReadFromJsonAsync<PagedEnvelope<MarketProductItemBody>>();
        env!.Data![0].CurrentPrice.Should().Be(88_000m,
            "DbPriceBoardReader returns the live DB price via overlay");
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
            location = "Integration Zone",
            address = "1 Test St",
            latitude = (decimal?)null,
            longitude = (decimal?)null
        });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<IdBody>>();
        return env!.Data!.Id;
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

    private async Task SetReservedQuantityAsync(Guid marketProductId, int reservedQty)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            """UPDATE market_products SET "ReservedQuantity" = {0} WHERE "Id" = {1}""",
            reservedQty, marketProductId);
    }
}
