using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Pricing;

/// <summary>
/// UC-PRI-04 — PATCH /api/v1/markets/{marketId}/products/{productId}/quantity integration tests.
/// Verifies authentication, RBAC, market assignment guard, 404, 409, 422, and successful updates.
/// </summary>
[Trait("Category", "Integration")]
public sealed class UpdateAvailableQuantityEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private static string Endpoint(Guid marketId, Guid productId) =>
        $"/api/v1/markets/{marketId}/products/{productId}/quantity";

    private readonly HttpClient _client = factory.CreateClient();

    // ── Authentication / Authorization ───────────────────────────────────────

    [Fact]
    public async Task PatchQuantity_Unauthenticated_Returns401Async()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PatchAsJsonAsync(
            Endpoint(Guid.NewGuid(), Guid.NewGuid()),
            new { quantity = 100 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PatchQuantity_AsAdmin_Returns403Async()
    {
        // Arrange — admin role does not have market_agent permission
        var token = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PatchAsJsonAsync(
            Endpoint(Guid.NewGuid(), Guid.NewGuid()),
            new { quantity = 100 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Market assignment guard ───────────────────────────────────────────────

    [Fact]
    public async Task PatchQuantity_AgentNotAssignedToMarket_Returns403WithMarketAccessDeniedAsync()
    {
        // Arrange — agent assigned to market1 but calls market2
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var market1Id = await CreateMarketAsync($"IT-QM1-{Guid.NewGuid():N}");
        var market2Id = await CreateMarketAsync($"IT-QM2-{Guid.NewGuid():N}");

        var agentEmail = $"agent-{Guid.NewGuid():N}@test.freshflow";
        await CreateMarketAgentAsync(agentEmail, market1Id);

        var agentToken = await LoginAsync(agentEmail, "AgentP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentToken);

        var response = await _client.PatchAsJsonAsync(
            Endpoint(market2Id, Guid.NewGuid()),
            new { quantity = 100 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Error!.Code.Should().Be("MARKET_ACCESS_DENIED");
    }

    // ── 404 product not found ─────────────────────────────────────────────────

    [Fact]
    public async Task PatchQuantity_ProductNotListedAtMarket_Returns404Async()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-QM-{Guid.NewGuid():N}");
        var agentEmail = $"agent-{Guid.NewGuid():N}@test.freshflow";
        await CreateMarketAgentAsync(agentEmail, marketId);

        var agentToken = await LoginAsync(agentEmail, "AgentP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentToken);

        var response = await _client.PatchAsJsonAsync(
            Endpoint(marketId, Guid.NewGuid()),
            new { quantity = 100 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Error!.Code.Should().Be("PRODUCT_NOT_FOUND");
    }

    // ── 422 business-rule violation ───────────────────────────────────────────

    [Fact]
    public async Task PatchQuantity_NegativeQuantity_Returns422WithInvalidQuantityAsync()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-QM-{Guid.NewGuid():N}");
        var agentEmail = $"agent-{Guid.NewGuid():N}@test.freshflow";
        await CreateMarketAgentAsync(agentEmail, marketId);

        var unitId = await GetOrCreateUnitAsync("kg");
        var productId = await CreateProductAsync($"Cua ghẹ {Guid.NewGuid():N}", unitId);
        await SeedMarketProductAsync(marketId, productId, 100_000m, 100);

        var agentToken = await LoginAsync(agentEmail, "AgentP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentToken);

        var response = await _client.PatchAsJsonAsync(
            Endpoint(marketId, productId),
            new { quantity = -1 });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Error!.Code.Should().Be("INVALID_QUANTITY");
    }

    // ── 200 successful update ─────────────────────────────────────────────────

    [Fact]
    public async Task PatchQuantity_ValidQuantity_Returns200WithCorrectDtoAsync()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-QM-{Guid.NewGuid():N}");
        var agentEmail = $"agent-{Guid.NewGuid():N}@test.freshflow";
        await CreateMarketAgentAsync(agentEmail, marketId);

        var unitId = await GetOrCreateUnitAsync("kg");
        var productId = await CreateProductAsync($"Mực ống {Guid.NewGuid():N}", unitId);
        await SeedMarketProductAsync(marketId, productId, 80_000m, 100);

        var agentToken = await LoginAsync(agentEmail, "AgentP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentToken);

        var response = await _client.PatchAsJsonAsync(
            Endpoint(marketId, productId),
            new { quantity = 500 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content
            .ReadFromJsonAsync<Envelope<UpdateQuantityResultBody>>();
        env!.Success.Should().BeTrue();
        env.Data!.CurrentQuantity.Should().Be(500);
        env.Data.PreviousQuantity.Should().Be(100);
        env.Data.IsOutOfStock.Should().BeFalse();
        env.Data.MarketId.Should().Be(marketId);
        env.Data.ProductId.Should().Be(productId);
    }

    [Fact]
    public async Task PatchQuantity_ZeroQuantity_Returns200WithIsOutOfStockTrueAsync()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-QM-{Guid.NewGuid():N}");
        var agentEmail = $"agent-{Guid.NewGuid():N}@test.freshflow";
        await CreateMarketAgentAsync(agentEmail, marketId);

        var unitId = await GetOrCreateUnitAsync("kg");
        var productId = await CreateProductAsync($"Nghêu sò {Guid.NewGuid():N}", unitId);
        await SeedMarketProductAsync(marketId, productId, 50_000m, 200);

        var agentToken = await LoginAsync(agentEmail, "AgentP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentToken);

        var response = await _client.PatchAsJsonAsync(
            Endpoint(marketId, productId),
            new { quantity = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content
            .ReadFromJsonAsync<Envelope<UpdateQuantityResultBody>>();
        env!.Success.Should().BeTrue();
        env.Data!.CurrentQuantity.Should().Be(0);
        env.Data.IsOutOfStock.Should().BeTrue();
    }

    // ── 409 optimistic concurrency conflict ───────────────────────────────────

    [Fact]
    public async Task PatchQuantity_StaleExpectedVersion_Returns409Async()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-QM-{Guid.NewGuid():N}");
        var agentEmail = $"agent-{Guid.NewGuid():N}@test.freshflow";
        await CreateMarketAgentAsync(agentEmail, marketId);

        var unitId = await GetOrCreateUnitAsync("kg");
        var productId = await CreateProductAsync($"Bạch tuộc {Guid.NewGuid():N}", unitId);
        await SeedMarketProductAsync(marketId, productId, 120_000m, 50);

        var agentToken = await LoginAsync(agentEmail, "AgentP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentToken);

        var staleVersion = DateTime.UtcNow.AddMinutes(-1).ToString("O");
        var response = await _client.PatchAsJsonAsync(
            Endpoint(marketId, productId),
            new { quantity = 100, expectedVersion = staleVersion });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Error!.Code.Should().Be("OPTIMISTIC_CONCURRENCY_CONFLICT");
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

    private async Task SeedMarketProductAsync(
        Guid marketId, Guid productId, decimal price, int quantity)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mp = new MarketProduct(marketId, productId, price, quantity, null);
        await db.Set<MarketProduct>().AddAsync(mp);
        await db.SaveChangesAsync();
    }
}

// ── Local response DTOs ───────────────────────────────────────────────────────

public sealed record UpdateQuantityResultBody(
    Guid MarketProductId,
    Guid ProductId,
    Guid MarketId,
    int PreviousQuantity,
    int CurrentQuantity,
    bool IsOutOfStock,
    DateTime UpdatedAt,
    Guid? UpdatedBy);
