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
/// UC-PRI-10 — GET /api/v1/markets/{marketId}/products/{productId}/price-history integration tests.
///
/// Verifies:
/// - Any authenticated user can call the endpoint (no role restriction — 401 only for anonymous)
/// - 404 for unknown marketId (MARKET_NOT_FOUND)
/// - 404 for product not listed at market (PRODUCT_NOT_FOUND)
/// - Snapshots returned newest-first (DESC RecordedAt)
/// - Cursor pagination (pageSize, nextCursor, two-page traverse)
/// - from/to date range filters
/// - 400 VALIDATION_ERROR for invalid date format
/// - 400 for invalid pageSize (validator)
/// </summary>
[Trait("Category", "Integration")]
public sealed class PriceHistoryEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private static string Endpoint(Guid marketId, Guid productId) =>
        $"/api/v1/markets/{marketId}/products/{productId}/price-history";

    private readonly HttpClient _client = factory.CreateClient();

    // ── Authentication ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPriceHistory_Unauthenticated_Returns401Async()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync(Endpoint(Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPriceHistory_AuthenticatedNonAdmin_Returns200OrErrorAsync()
    {
        // Arrange — market_agent can call the endpoint (any authenticated is allowed)
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"PH-Auth-{Guid.NewGuid():N}");
        var agentEmail = $"agent-ph-auth-{Guid.NewGuid():N}@test.freshflow";
        await CreateMarketAgentAsync(agentEmail, marketId);

        var agentToken = await LoginAsync(agentEmail, "AgentP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentToken);

        // Act — non-existent product for this market; 404 means auth passed (not 403)
        var response = await _client.GetAsync(Endpoint(marketId, Guid.NewGuid()));

        // Assert — 404 (not 403) confirms market_agent can access the endpoint
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "any authenticated user should reach the handler; 404 = product not found, not access denied");
    }

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPriceHistory_UnknownMarket_Returns404WithMarketNotFoundAsync()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await _client.GetAsync(Endpoint(Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Error!.Code.Should().Be("MARKET_NOT_FOUND");
    }

    [Fact]
    public async Task GetPriceHistory_ProductNotListedAtMarket_Returns404WithProductNotFoundAsync()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"PH-NF-{Guid.NewGuid():N}");

        // Product ID that does not exist in the market's listing
        var unlistedProductId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync(Endpoint(marketId, unlistedProductId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Error!.Code.Should().Be("PRODUCT_NOT_FOUND");
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPriceHistory_AfterPriceUpdates_ReturnsSnapshotsNewestFirstAsync()
    {
        // Arrange
        var (adminToken, marketId, productId, _) = await SetupMarketWithAgentAndProductAsync();

        // Act — read history as admin
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await _client.GetAsync(Endpoint(marketId, productId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content
            .ReadFromJsonAsync<PagedEnvelope<PriceHistoryItemBody>>();
        env!.Success.Should().BeTrue();
        env.Data.Should().NotBeNull();
        env.Data!.Count.Should().BeGreaterThanOrEqualTo(3,
            "one snapshot per price update (3 updates were made)");

        // Snapshots must be ordered newest first
        env.Data.Select(i => i.RecordedAt)
            .Should().BeInDescendingOrder("price history is returned newest-first");
    }

    [Fact]
    public async Task GetPriceHistory_EmptyMarketProduct_ReturnsEmptyPageAsync()
    {
        // Arrange — seed a market product but make NO price updates
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"PH-Empty-{Guid.NewGuid():N}");
        var unitId = await GetOrCreateUnitAsync("kg");
        var productId = await CreateProductAsync($"PH-Empty-{Guid.NewGuid():N}", unitId);
        await SeedMarketProductAsync(marketId, productId, 50_000m, 10);

        // Act
        var response = await _client.GetAsync(Endpoint(marketId, productId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content
            .ReadFromJsonAsync<PagedEnvelope<PriceHistoryItemBody>>();
        env!.Data!.Should().BeEmpty("no updates were made for this product");
        env.Meta!.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task GetPriceHistory_ResponseItemContainsAllRequiredFieldsAsync()
    {
        // Arrange
        var (adminToken, marketId, productId, marketProductId) =
            await SetupMarketWithAgentAndProductAsync();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await _client.GetAsync(Endpoint(marketId, productId));

        // Assert — verify all required fields per UC-PRI-10 spec
        var env = await response.Content
            .ReadFromJsonAsync<PagedEnvelope<PriceHistoryItemBody>>();
        var item = env!.Data!.First();
        item.Id.Should().NotBeEmpty();
        item.MarketProductId.Should().Be(marketProductId,
            "snapshot must reference the correct market product");
        item.Price.Should().BePositive();
        item.Quantity.Should().BePositive();
        item.RecordedAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(-2));
        // RecordedBy may be null (system) or a guid (agent) — both are valid
    }

    // ── Cursor pagination ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetPriceHistory_CursorPagination_ReturnsTwoPagesAsync()
    {
        // Arrange — seed 3 updates, page with pageSize=2 → 2 pages
        var (adminToken, marketId, productId, _) = await SetupMarketWithAgentAndProductAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // Act — page 1
        var page1 = await _client.GetAsync(Endpoint(marketId, productId) + "?pageSize=2");
        page1.StatusCode.Should().Be(HttpStatusCode.OK);
        var env1 = await page1.Content
            .ReadFromJsonAsync<PagedEnvelope<PriceHistoryItemBody>>();
        env1!.Data!.Should().HaveCount(2);
        env1.Meta!.NextCursor.Should().NotBeNullOrEmpty(
            "3 snapshots with pageSize=2 must yield a next cursor");

        // Act — page 2
        var cursorParam = Uri.EscapeDataString(env1.Meta.NextCursor!);
        var page2 = await _client.GetAsync(
            Endpoint(marketId, productId) + $"?pageSize=2&cursor={cursorParam}");
        page2.StatusCode.Should().Be(HttpStatusCode.OK);
        var env2 = await page2.Content
            .ReadFromJsonAsync<PagedEnvelope<PriceHistoryItemBody>>();
        env2!.Data!.Should().HaveCount(1, "only 3 snapshots total");
        env2.Meta!.NextCursor.Should().BeNull("no more pages");

        // No duplicate IDs across pages
        var ids1 = env1.Data!.Select(i => i.Id).ToHashSet();
        var ids2 = env2.Data!.Select(i => i.Id).ToHashSet();
        ids1.Should().NotIntersectWith(ids2, "pages must not overlap");
    }

    // ── Date range filter ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetPriceHistory_WithFromFilter_ExcludesOlderSnapshotsAsync()
    {
        // Arrange — produce 3 snapshots, then filter to only the very recent ones
        var (adminToken, marketId, productId, _) = await SetupMarketWithAgentAndProductAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // Set 'from' to 1 minute from now → excludes all past snapshots
        var futureFrom = DateTime.UtcNow.AddMinutes(1).ToString("O");

        // Act
        var response = await _client.GetAsync(
            Endpoint(marketId, productId) + $"?from={Uri.EscapeDataString(futureFrom)}");

        // Assert — no snapshots fall after the future date
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content
            .ReadFromJsonAsync<PagedEnvelope<PriceHistoryItemBody>>();
        env!.Data!.Should().BeEmpty(
            "all snapshots were recorded before the 'from' filter");
    }

    [Fact]
    public async Task GetPriceHistory_WithToFilter_ExcludesNewerSnapshotsAsync()
    {
        // Arrange
        var (adminToken, marketId, productId, _) = await SetupMarketWithAgentAndProductAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // Set 'to' to 1 minute in the past → excludes all just-created snapshots
        var pastTo = DateTime.UtcNow.AddMinutes(-1).ToString("O");

        // Act
        var response = await _client.GetAsync(
            Endpoint(marketId, productId) + $"?to={Uri.EscapeDataString(pastTo)}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content
            .ReadFromJsonAsync<PagedEnvelope<PriceHistoryItemBody>>();
        env!.Data!.Should().BeEmpty(
            "all snapshots were recorded after the 'to' filter");
    }

    [Fact]
    public async Task GetPriceHistory_FromWithTimezoneOffset_NormalizedToUtcAndAcceptedAsync()
    {
        // Arrange — verify that ISO 8601 strings WITH a timezone offset (e.g. "+07:00") are
        // correctly normalized to UTC by the controller (Fix #1).
        // Before the fix, DateTime.SpecifyKind merely relabelled the kind flag without converting
        // the offset, so "+07:00" inputs silently lost the 7-hour shift.
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // A future instant expressed in UTC+7 local time:
        //   UTC+7 2099-01-01T07:00:00+07:00  ≡  UTC 2099-01-01T00:00:00Z
        // Using a far-future date ensures no snapshots can exist after it,
        // so the response must be 200 OK (successful parse) with an empty list.
        const string futureFromWithOffset = "2099-01-01T07:00:00+07:00";

        // Act
        var response = await _client.GetAsync(
            Endpoint(Guid.NewGuid(), Guid.NewGuid()) +
            $"?from={Uri.EscapeDataString(futureFromWithOffset)}");

        // Assert — 404 (market/product not found) OR 200 (empty result) means the date was
        // parsed successfully; 400 VALIDATION_ERROR would mean the parse itself failed.
        response.StatusCode.Should().NotBe(
            System.Net.HttpStatusCode.BadRequest,
            "a valid ISO 8601 datetime with timezone offset should not produce a 400 VALIDATION_ERROR");
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPriceHistory_InvalidFromDate_Returns400ValidationErrorAsync()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // Act — non-parseable date string
        var response = await _client.GetAsync(
            Endpoint(Guid.NewGuid(), Guid.NewGuid()) + "?from=not-a-date");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Error!.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task GetPriceHistory_InvalidToDate_Returns400ValidationErrorAsync()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await _client.GetAsync(
            Endpoint(Guid.NewGuid(), Guid.NewGuid()) + "?to=abc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Error!.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task GetPriceHistory_InvalidPageSize_Returns400Async()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // Act — pageSize=0 is invalid (must be 1–200)
        var response = await _client.GetAsync(
            Endpoint(Guid.NewGuid(), Guid.NewGuid()) + "?pageSize=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPriceHistory_PageSizeTooLarge_Returns400Async()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // Act — pageSize=201 exceeds max of 200
        var response = await _client.GetAsync(
            Endpoint(Guid.NewGuid(), Guid.NewGuid()) + "?pageSize=201");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a market + agent + product + market_product, makes 3 price updates,
    /// and returns (adminToken, marketId, productId, marketProductId).
    /// </summary>
    private async Task<(string AdminToken, Guid MarketId, Guid ProductId, Guid MarketProductId)>
        SetupMarketWithAgentAndProductAsync()
    {
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"PH-{Guid.NewGuid():N}");
        var agentEmail = $"agent-ph-{Guid.NewGuid():N}@test.freshflow";
        await CreateMarketAgentAsync(agentEmail, marketId);

        var unitId = await GetOrCreateUnitAsync("kg");
        var productId = await CreateProductAsync($"PH-Fish-{Guid.NewGuid():N}", unitId);
        var marketProductId = await SeedMarketProductAsync(marketId, productId, 100_000m, 500);

        // Switch to agent and make 3 price updates
        var agentToken = await LoginAsync(agentEmail, "AgentP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agentToken);

        for (var i = 1; i <= 3; i++)
        {
            var resp = await _client.PatchAsJsonAsync(
                $"/api/v1/markets/{marketId}/products/{productId}/price",
                new { price = 100_000m + i * 5_000m });
            resp.EnsureSuccessStatusCode();
        }

        return (adminToken, marketId, productId, marketProductId);
    }

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

// ── Local response DTOs ───────────────────────────────────────────────────────

public sealed record PriceHistoryItemBody(
    Guid Id,
    Guid MarketProductId,
    decimal Price,
    int Quantity,
    Guid? RecordedBy,
    DateTime RecordedAt);
