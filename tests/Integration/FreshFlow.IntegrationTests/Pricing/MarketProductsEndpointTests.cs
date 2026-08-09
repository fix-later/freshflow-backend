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
/// UC-PRI-02 — GET /api/v1/markets/{marketId}/products integration tests.
/// Verifies authentication, 404 for non-existent markets, cursor pagination,
/// and correct data projection including product name, unit, and category.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MarketProductsEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private static string Endpoint(Guid marketId) =>
        $"/api/v1/markets/{marketId}/products";

    private readonly HttpClient _client = factory.CreateClient();

    // ── Authentication ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMarketProducts_Unauthenticated_IsAllowed_PublicHomepage()
    {
        // Endpoint is [AllowAnonymous] for guest homepage browsing, so an anonymous request
        // passes auth and falls through to the handler — here a 404 for a non-existent market
        // (never a 401).
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync(Endpoint(Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── 404 market not found ──────────────────────────────────────────────────

    [Fact]
    public async Task GetMarketProducts_MarketNotFound_Returns404Async()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // Act — non-existent market ID
        var response = await _client.GetAsync(Endpoint(Guid.NewGuid()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env!.Success.Should().BeFalse();
        env.Error!.Code.Should().Be("MARKET_NOT_FOUND");
    }

    // ── 200 empty list ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMarketProducts_ValidMarketNoProducts_Returns200EmptyListAsync()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-Mkt-{Guid.NewGuid():N}");

        // Act
        var response = await _client.GetAsync(Endpoint(marketId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content
            .ReadFromJsonAsync<PagedEnvelope<MarketProductItemBody>>();
        env.Should().NotBeNull();
        env!.Success.Should().BeTrue();
        env.Data.Should().NotBeNull();
        env.Data!.Should().BeEmpty();
        env.Meta.Should().NotBeNull();
        env.Meta!.PageSize.Should().Be(20);
        env.Meta.NextCursor.Should().BeNull();
    }

    // ── 200 with products ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetMarketProducts_WithSeededProducts_Returns200WithDataAsync()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        // Create market
        var marketId = await CreateMarketAsync($"IT-Mkt-{Guid.NewGuid():N}");

        // Create a product via Catalog API
        var unitId = await GetOrCreateUnitAsync("kg");
        const string imageUrl = "https://images.example.com/ca-loc.jpg";
        var productId = await CreateProductAsync($"Cá lóc {Guid.NewGuid():N}", unitId);
        await SetProductImageUrlAsync(productId, imageUrl);

        // Seed a market_product directly (no create-market-product endpoint yet)
        await SeedMarketProductAsync(marketId, productId, 125_000m, 500);

        // Act
        var response = await _client.GetAsync(Endpoint(marketId));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content
            .ReadFromJsonAsync<PagedEnvelope<MarketProductItemBody>>();
        env!.Success.Should().BeTrue();
        env.Data.Should().ContainSingle();
        var item = env.Data![0];
        item.MarketId.Should().Be(marketId);
        item.ProductId.Should().Be(productId);
        item.ImageUrl.Should().Be(imageUrl);
        item.CurrentPrice.Should().Be(125_000m);
        item.CurrentQuantity.Should().Be(500);
        item.AvailableQuantity.Should().Be(500);
        item.Unit.Should().Be("kg");
        env.Meta!.PageSize.Should().Be(20);
    }

    // ── Cursor pagination ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetMarketProducts_PageSizeOne_NextCursorPresentAsync()
    {
        // Arrange — seed two products so page 1 has nextCursor
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-Mkt-{Guid.NewGuid():N}");
        var unitId = await GetOrCreateUnitAsync("kg");
        var product1Id = await CreateProductAsync($"Product1-{Guid.NewGuid():N}", unitId);
        var product2Id = await CreateProductAsync($"Product2-{Guid.NewGuid():N}", unitId);

        await SeedMarketProductAsync(marketId, product1Id, 10_000m, 10);
        await SeedMarketProductAsync(marketId, product2Id, 20_000m, 20);

        // Act — page 1 with pageSize=1
        var response = await _client.GetAsync(Endpoint(marketId) + "?pageSize=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content
            .ReadFromJsonAsync<PagedEnvelope<MarketProductItemBody>>();
        env!.Data!.Should().HaveCount(1);
        env.Meta!.NextCursor.Should().NotBeNullOrEmpty();

        // Act — page 2 using nextCursor
        var nextCursor = Uri.EscapeDataString(env.Meta.NextCursor!);
        var page2Response = await _client.GetAsync(
            Endpoint(marketId) + $"?pageSize=1&cursor={nextCursor}");
        var page2Env = await page2Response.Content
            .ReadFromJsonAsync<PagedEnvelope<MarketProductItemBody>>();

        page2Env!.Data!.Should().HaveCount(1);
        page2Env.Data![0].ProductId.Should().NotBe(env.Data[0].ProductId);
        page2Env.Meta!.NextCursor.Should().BeNull(); // no more pages
    }

    // ── Cursor correctness when UpdatedAt > CreatedAt ─────────────────────────

    /// <summary>
    /// Regression test for cursor encoding bug:
    /// Cursor must encode CreatedAt (the sort key), NOT UpdatedAt.
    /// When a product's price has been updated, UpdatedAt drifts far from CreatedAt.
    /// If the cursor encoded UpdatedAt, page 2's WHERE clause (CreatedAt > cursor.createdAt)
    /// would compare CreatedAt against a future UpdatedAt, producing an empty page.
    /// </summary>
    [Fact]
    public async Task GetMarketProducts_ProductWithUpdatedAtAfterCreatedAt_CursorPaginatesCorrectlyAsync()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-Mkt-Cursor-{Guid.NewGuid():N}");
        var unitId = await GetOrCreateUnitAsync("kg");
        var product1Id = await CreateProductAsync($"Cursor-P1-{Guid.NewGuid():N}", unitId);
        var product2Id = await CreateProductAsync($"Cursor-P2-{Guid.NewGuid():N}", unitId);

        var mp1Id = await SeedMarketProductAsync(marketId, product1Id, 100_000m, 50);
        await SeedMarketProductAsync(marketId, product2Id, 200_000m, 100);

        // Simulate a price update: set mp1.UpdatedAt far in the future
        // so UpdatedAt >> CreatedAt. A buggy cursor would encode this future UpdatedAt,
        // making page 2's WHERE (CreatedAt > futureDate) return zero rows.
        await BumpUpdatedAtAsync(mp1Id, DateTime.UtcNow.AddYears(1));

        // Act — page 1 (pageSize=1)
        var page1 = await _client.GetAsync(Endpoint(marketId) + "?pageSize=1");
        page1.StatusCode.Should().Be(HttpStatusCode.OK);
        var env1 = await page1.Content.ReadFromJsonAsync<PagedEnvelope<MarketProductItemBody>>();
        env1!.Data!.Should().HaveCount(1);
        env1.Meta!.NextCursor.Should().NotBeNullOrEmpty("page 1 of 2 must have a nextCursor");

        // Act — page 2 using cursor from page 1
        var cursorParam = Uri.EscapeDataString(env1.Meta.NextCursor!);
        var page2 = await _client.GetAsync(Endpoint(marketId) + $"?pageSize=1&cursor={cursorParam}");

        // Assert — page 2 must NOT be empty
        page2.StatusCode.Should().Be(HttpStatusCode.OK);
        var env2 = await page2.Content.ReadFromJsonAsync<PagedEnvelope<MarketProductItemBody>>();
        env2!.Data!.Should().HaveCount(1,
            "cursor must encode CreatedAt (sort key) not UpdatedAt (which can be > all other CreatedAt values)");
        env2.Data![0].ProductId.Should().NotBe(env1.Data[0].ProductId);
        env2.Meta!.NextCursor.Should().BeNull("only 2 products total");
    }

    // ── Featured-first (page 1 only) ──────────────────────────────────────────

    /// <summary>
    /// A listing tagged with MarketProduct.FeaturedTag ("nổi bật") is pinned to the top of
    /// page 1 and appears exactly once — it must NOT reappear in the keyset-paginated stream
    /// on page 2.
    /// </summary>
    [Fact]
    public async Task GetMarketProducts_FeaturedTag_PinnedToTopOfPage1OnlyAsync()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-Mkt-Feat-{Guid.NewGuid():N}");
        var unitId = await GetOrCreateUnitAsync("kg");
        var plain1Id = await CreateProductAsync($"Feat-Plain1-{Guid.NewGuid():N}", unitId);
        var plain2Id = await CreateProductAsync($"Feat-Plain2-{Guid.NewGuid():N}", unitId);
        var featuredId = await CreateProductAsync($"Feat-Star-{Guid.NewGuid():N}", unitId);

        var pinnedTag = await CreateTagAsync($"nổi bật {Guid.NewGuid().ToString("N")[..6]}", pinsToTop: true);

        // Two plain products, then one featured (created last → latest CreatedAt).
        await SeedMarketProductAsync(marketId, plain1Id, 10_000m, 10);
        await SeedMarketProductAsync(marketId, plain2Id, 20_000m, 20);
        await SeedMarketProductAsync(marketId, featuredId, 30_000m, 30, tagIds: [pinnedTag]);

        // Act — page 1 with pageSize=1
        var page1 = await _client.GetAsync(Endpoint(marketId) + "?pageSize=1");
        page1.StatusCode.Should().Be(HttpStatusCode.OK);
        var env1 = await page1.Content.ReadFromJsonAsync<PagedEnvelope<MarketProductItemBody>>();

        // Assert — featured is pinned first despite being created last; plus one plain item.
        env1!.Data!.Should().HaveCount(2, "featured item is prepended on top of the pageSize page");
        env1.Data![0].ProductId.Should().Be(featuredId);
        env1.Data[0].Tags.Should().ContainSingle(t => t.Id == pinnedTag && t.PinsToTop);
        env1.Data[1].Tags.Should().BeEmpty();
        env1.Meta!.NextCursor.Should().NotBeNullOrEmpty("one plain product remains for page 2");

        // Act — page 2 via cursor
        var cursor = Uri.EscapeDataString(env1.Meta.NextCursor!);
        var page2 = await _client.GetAsync(Endpoint(marketId) + $"?pageSize=1&cursor={cursor}");
        var env2 = await page2.Content.ReadFromJsonAsync<PagedEnvelope<MarketProductItemBody>>();

        // Assert — featured does NOT reappear; only the remaining plain product.
        env2!.Data!.Should().HaveCount(1);
        env2.Data![0].ProductId.Should().NotBe(featuredId);
        env2.Data[0].Tags.Should().BeEmpty();
        env2.Meta!.NextCursor.Should().BeNull("both plain products have now been paged");
    }

    // ── ?tag= filter ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMarketProducts_TagFilter_ReturnsOnlyMatchingListingsAsync()
    {
        // Arrange
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var marketId = await CreateMarketAsync($"IT-Mkt-Tag-{Guid.NewGuid():N}");
        var unitId = await GetOrCreateUnitAsync("kg");
        var promoId = await CreateProductAsync($"Tag-Promo-{Guid.NewGuid():N}", unitId);
        var plainId = await CreateProductAsync($"Tag-Plain-{Guid.NewGuid():N}", unitId);

        var promoTagName = $"khuyến mãi {Guid.NewGuid().ToString("N")[..6]}";
        var promoTagId = await CreateTagAsync(promoTagName, pinsToTop: false);

        await SeedMarketProductAsync(marketId, promoId, 10_000m, 10, tagIds: [promoTagId]);
        await SeedMarketProductAsync(marketId, plainId, 20_000m, 20);

        // Act
        var tagParam = Uri.EscapeDataString(promoTagName);
        var response = await _client.GetAsync(Endpoint(marketId) + $"?tag={tagParam}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content.ReadFromJsonAsync<PagedEnvelope<MarketProductItemBody>>();

        // Assert — only the tagged listing is returned.
        env!.Data!.Should().ContainSingle();
        env.Data![0].ProductId.Should().Be(promoId);
        env.Data[0].Tags.Should().ContainSingle(t => t.Id == promoTagId);
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
        // Try to get existing unit first
        var listResp = await _client.GetAsync("/api/v1/units");
        listResp.EnsureSuccessStatusCode();
        var listEnv = await listResp.Content.ReadFromJsonAsync<Envelope<List<UnitBody>>>();
        var existing = listEnv!.Data?.FirstOrDefault(u =>
            u.Name.Equals(unitName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return existing.Id;

        // Create new unit
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

    private async Task SetProductImageUrlAsync(Guid productId, string imageUrl)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            """UPDATE products SET "ImageUrl" = {0} WHERE "Id" = {1}""",
            imageUrl, productId);
    }

    private async Task<Guid> SeedMarketProductAsync(
        Guid marketId, Guid productId, decimal price, int quantity, IReadOnlyList<Guid>? tagIds = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mp = new MarketProduct(marketId, productId, price, quantity, null);
        if (tagIds is not null)
        {
            var tags = await db.Set<Tag>().Where(t => tagIds.Contains(t.Id)).ToListAsync();
            mp.SetTags(tags, null);
        }
        await db.Set<MarketProduct>().AddAsync(mp);
        await db.SaveChangesAsync();
        return mp.Id;
    }

    private async Task<Guid> CreateTagAsync(string name, bool pinsToTop)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/tags", new { name, pinsToTop });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<IdBody>>();
        return env!.Data!.Id;
    }

    /// <summary>
    /// Directly sets UpdatedAt on a market_products row to simulate a price update
    /// that occurred after the initial creation — making UpdatedAt diverge from CreatedAt.
    /// </summary>
    private async Task BumpUpdatedAtAsync(Guid marketProductId, DateTime updatedAt)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            """UPDATE market_products SET "UpdatedAt" = {0} WHERE "Id" = {1}""",
            updatedAt, marketProductId);
    }
}

// ── Local response DTOs ───────────────────────────────────────────────────────

public sealed record PagedEnvelope<T>(bool Success, List<T>? Data, PaginationMetaBody? Meta);

public sealed record PaginationMetaBody(int PageSize, string? NextCursor);

public sealed record MarketProductItemBody(
    Guid MarketProductId,
    Guid ProductId,
    Guid MarketId,
    string ProductName,
    string? ImageUrl,
    string? Category,
    string Unit,
    decimal CurrentPrice,
    int CurrentQuantity,
    int AvailableQuantity,
    IReadOnlyList<MarketProductTagBody> Tags,
    DateTime UpdatedAt,
    Guid? UpdatedBy);

public sealed record MarketProductTagBody(Guid Id, string Name, bool PinsToTop);

public sealed record IdBody(Guid Id);

public sealed record UnitBody(Guid Id, string Name);
