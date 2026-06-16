using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Pricing;

/// <summary>
/// SCRUM-178 — POST /api/v1/markets/{marketId}/products integration tests.
/// Verifies authentication, RBAC (admin only), 404 (market/product), 409 (duplicate listing),
/// 422 (invalid price), and successful creation.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CreateMarketProductEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private static string Endpoint(Guid marketId) => $"/api/v1/markets/{marketId}/products";

    private readonly HttpClient _client = factory.CreateClient();

    // ── Authentication / Authorization ───────────────────────────────────────

    [Fact]
    public async Task PostMarketProduct_Unauthenticated_Returns401Async()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.PostAsJsonAsync(
            Endpoint(Guid.NewGuid()),
            new { productId = Guid.NewGuid(), initialPrice = 25_000m, initialQuantity = 100 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostMarketProduct_AsMarketAgent_Returns403Async()
    {
        // Arrange — market_agent is not authorized to list new products (admin only)
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-CMP-M-{Guid.NewGuid():N}");
        var agentEmail = $"agent-{Guid.NewGuid():N}@test.freshflow";
        await CreateMarketAgentAsync(agentEmail, marketId);

        var agentToken = await LoginAsync(agentEmail, "AgentP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentToken);

        // Act
        var response = await _client.PostAsJsonAsync(
            Endpoint(marketId),
            new { productId = Guid.NewGuid(), initialPrice = 25_000m, initialQuantity = 100 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── 404 market not found ───────────────────────────────────────────────────

    [Fact]
    public async Task PostMarketProduct_MarketNotFound_Returns404Async()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var unitId = await GetOrCreateUnitAsync("kg");
        var productId = await CreateProductAsync($"Cá basa {Guid.NewGuid():N}", unitId);

        // Act — non-existent marketId
        var response = await _client.PostAsJsonAsync(
            Endpoint(Guid.NewGuid()),
            new { productId, initialPrice = 25_000m, initialQuantity = 100 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Error!.Code.Should().Be("MARKET_NOT_FOUND");
    }

    // ── 404 product not found ──────────────────────────────────────────────────

    [Fact]
    public async Task PostMarketProduct_ProductNotFound_Returns404Async()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-CMP-M-{Guid.NewGuid():N}");

        // Act — non-existent productId
        var response = await _client.PostAsJsonAsync(
            Endpoint(marketId),
            new { productId = Guid.NewGuid(), initialPrice = 25_000m, initialQuantity = 100 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Error!.Code.Should().Be("PRODUCT_NOT_FOUND");
    }

    // ── 409 duplicate listing ─────────────────────────────────────────────────

    [Fact]
    public async Task PostMarketProduct_AlreadyListed_Returns409Async()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-CMP-M-{Guid.NewGuid():N}");
        var unitId = await GetOrCreateUnitAsync("kg");
        var productId = await CreateProductAsync($"Tôm sú {Guid.NewGuid():N}", unitId);

        var firstResponse = await _client.PostAsJsonAsync(
            Endpoint(marketId),
            new { productId, initialPrice = 25_000m, initialQuantity = 100 });
        firstResponse.EnsureSuccessStatusCode();

        // Act — list the same product at the same market again
        var response = await _client.PostAsJsonAsync(
            Endpoint(marketId),
            new { productId, initialPrice = 30_000m, initialQuantity = 50 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Error!.Code.Should().Be("MARKET_PRODUCT_ALREADY_EXISTS");
    }

    // ── 422 invalid price ─────────────────────────────────────────────────────

    [Fact]
    public async Task PostMarketProduct_InvalidPrice_Returns422Async()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-CMP-M-{Guid.NewGuid():N}");
        var unitId = await GetOrCreateUnitAsync("kg");
        var productId = await CreateProductAsync($"Mực ống {Guid.NewGuid():N}", unitId);

        // Act — initialPrice = 0 is a 422 business rule
        var response = await _client.PostAsJsonAsync(
            Endpoint(marketId),
            new { productId, initialPrice = 0m, initialQuantity = 100 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Error!.Code.Should().Be("INVALID_PRICE");
    }

    // ── 201 success ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PostMarketProduct_ValidRequest_Returns201WithCorrectDtoAsync()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-CMP-M-{Guid.NewGuid():N}");
        var unitId = await GetOrCreateUnitAsync("kg");
        var productId = await CreateProductAsync($"Cá thu {Guid.NewGuid():N}", unitId);

        // Act
        var response = await _client.PostAsJsonAsync(
            Endpoint(marketId),
            new { productId, initialPrice = 45_000m, initialQuantity = 250 });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var env = await response.Content
            .ReadFromJsonAsync<Envelope<CreateMarketProductResultBody>>();
        env!.Success.Should().BeTrue();
        env.Data!.MarketId.Should().Be(marketId);
        env.Data.ProductId.Should().Be(productId);
        env.Data.CurrentPrice.Should().Be(45_000m);
        env.Data.CurrentQuantity.Should().Be(250);
        env.Data.MarketProductId.Should().NotBe(Guid.Empty);
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
}

// ── Local response DTOs ───────────────────────────────────────────────────────

public sealed record CreateMarketProductResultBody(
    Guid MarketProductId,
    Guid MarketId,
    Guid ProductId,
    decimal CurrentPrice,
    int CurrentQuantity,
    DateTime CreatedAt,
    Guid? CreatedBy);
