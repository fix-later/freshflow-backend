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
/// UC-PRI-03 — PATCH /api/v1/markets/{marketId}/products/{productId}/price integration tests.
/// Verifies authentication, RBAC, market assignment guard, 404, 409, and successful updates.
/// </summary>
[Trait("Category", "Integration")]
public sealed class UpdateProductPriceEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private static string Endpoint(Guid marketId, Guid productId) =>
        $"/api/v1/markets/{marketId}/products/{productId}/price";

    private readonly HttpClient _client = factory.CreateClient();

    // ── Authentication / Authorization ───────────────────────────────────────

    [Fact]
    public async Task PatchPrice_Unauthenticated_Returns401()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.PatchAsJsonAsync(
            Endpoint(Guid.NewGuid(), Guid.NewGuid()),
            new { price = 100_000m });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PatchPrice_AsAdmin_Returns403()
    {
        // Arrange — admin does not have market_agent role
        var token = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PatchAsJsonAsync(
            Endpoint(Guid.NewGuid(), Guid.NewGuid()),
            new { price = 100_000m });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Market assignment guard ───────────────────────────────────────────────

    [Fact]
    public async Task PatchPrice_AgentNotAssignedToMarket_Returns403WithMarketAccessDenied()
    {
        // Arrange — create two markets, agent is assigned to market1 but tries to update market2
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var market1Id = await CreateMarketAsync($"IT-M1-{Guid.NewGuid():N}");
        var market2Id = await CreateMarketAsync($"IT-M2-{Guid.NewGuid():N}");

        var agentEmail = $"agent-{Guid.NewGuid():N}@test.freshflow";
        await CreateMarketAgentAsync(agentEmail, market1Id);

        var agentToken = await LoginAsync(agentEmail, "AgentP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentToken);

        // Act — agent tries to update price in market2 (not assigned)
        var response = await _client.PatchAsJsonAsync(
            Endpoint(market2Id, Guid.NewGuid()),
            new { price = 100_000m });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Error!.Code.Should().Be("MARKET_ACCESS_DENIED");
    }

    // ── 404 product not found ─────────────────────────────────────────────────

    [Fact]
    public async Task PatchPrice_ProductNotListedAtMarket_Returns404()
    {
        // Arrange — agent is assigned to market, but no market_product for productId
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-M-{Guid.NewGuid():N}");
        var agentEmail = $"agent-{Guid.NewGuid():N}@test.freshflow";
        await CreateMarketAgentAsync(agentEmail, marketId);

        var agentToken = await LoginAsync(agentEmail, "AgentP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentToken);

        // Act — non-existent productId
        var response = await _client.PatchAsJsonAsync(
            Endpoint(marketId, Guid.NewGuid()),
            new { price = 100_000m });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Error!.Code.Should().Be("PRODUCT_NOT_FOUND");
    }

    // ── 400 validation ────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchPrice_NeitherPriceNorQuantity_Returns400()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-M-{Guid.NewGuid():N}");
        var agentEmail = $"agent-{Guid.NewGuid():N}@test.freshflow";
        await CreateMarketAgentAsync(agentEmail, marketId);

        var agentToken = await LoginAsync(agentEmail, "AgentP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentToken);

        // Act — neither price nor quantity provided
        var response = await _client.PatchAsJsonAsync(
            Endpoint(marketId, Guid.NewGuid()),
            new { });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── 200 successful price update ───────────────────────────────────────────

    [Fact]
    public async Task PatchPrice_ValidPriceUpdate_Returns200WithCorrectDto()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-M-{Guid.NewGuid():N}");
        var agentEmail = $"agent-{Guid.NewGuid():N}@test.freshflow";
        await CreateMarketAgentAsync(agentEmail, marketId);

        var unitId = await GetOrCreateUnitAsync("kg");
        var productId = await CreateProductAsync($"Cá thu {Guid.NewGuid():N}", unitId);
        await SeedMarketProductAsync(marketId, productId, 100_000m, 500);

        var agentToken = await LoginAsync(agentEmail, "AgentP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentToken);

        // Act
        var response = await _client.PatchAsJsonAsync(
            Endpoint(marketId, productId),
            new { price = 120_000m });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content
            .ReadFromJsonAsync<Envelope<UpdatePriceResultBody>>();
        env!.Success.Should().BeTrue();
        env.Data!.CurrentPrice.Should().Be(120_000m);
        env.Data.PreviousPrice.Should().Be(100_000m);
        env.Data.ChangePercent.Should().Be(20.00m);
        env.Data.CurrentQuantity.Should().Be(500); // unchanged
        env.Data.MarketId.Should().Be(marketId);
        env.Data.ProductId.Should().Be(productId);
        env.Data.SnapshotId.Should().NotBe(Guid.Empty);
    }

    // ── 409 optimistic concurrency conflict ───────────────────────────────────

    [Fact]
    public async Task PatchPrice_StaleExpectedVersion_Returns409()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-M-{Guid.NewGuid():N}");
        var agentEmail = $"agent-{Guid.NewGuid():N}@test.freshflow";
        await CreateMarketAgentAsync(agentEmail, marketId);

        var unitId = await GetOrCreateUnitAsync("kg");
        var productId = await CreateProductAsync($"Tôm sú {Guid.NewGuid():N}", unitId);
        await SeedMarketProductAsync(marketId, productId, 200_000m, 100);

        var agentToken = await LoginAsync(agentEmail, "AgentP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentToken);

        // Act — provide a stale expectedVersion (1 minute in the past)
        var staleVersion = DateTime.UtcNow.AddMinutes(-1).ToString("O");
        var response = await _client.PatchAsJsonAsync(
            Endpoint(marketId, productId),
            new { price = 220_000m, expectedVersion = staleVersion });

        // Assert
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

public sealed record UpdatePriceResultBody(
    Guid MarketProductId,
    Guid ProductId,
    Guid MarketId,
    decimal PreviousPrice,
    decimal CurrentPrice,
    int CurrentQuantity,
    decimal ChangePercent,
    DateTime UpdatedAt,
    Guid? UpdatedBy,
    Guid SnapshotId);
