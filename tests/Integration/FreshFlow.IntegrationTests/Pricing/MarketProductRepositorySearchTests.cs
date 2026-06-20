using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Pricing;

/// <summary>
/// ASSIST-E1-T3 — <see cref="IMarketProductRepository.SearchAsync"/> integration tests.
/// Requires a real PostgreSQL provider (ILIKE, ToSqlQuery join) so this is covered at the
/// integration level rather than with the EF InMemory provider.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MarketProductRepositorySearchTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SearchAsync_MatchByPartialName_ReturnsMatchingItemAsync()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        var marketId = await CreateMarketAsync();
        var unitId = await GetOrCreateUnitAsync("kg");
        var matchingProductId = await CreateProductAsync($"Cá lóc {Guid.NewGuid():N}", unitId);
        var otherProductId = await CreateProductAsync($"Tôm sú {Guid.NewGuid():N}", unitId);
        await SeedMarketProductAsync(marketId, matchingProductId, 100_000m, 50);
        await SeedMarketProductAsync(marketId, otherProductId, 200_000m, 30);

        var repository = GetRepository();

        // Act
        var (items, nextCursor) = await repository.SearchAsync(
            new MarketProductSearchCriteria(marketId, "Cá lóc", null, false, null, 20),
            CancellationToken.None);

        // Assert
        items.Should().ContainSingle();
        items[0].ProductId.Should().Be(matchingProductId);
        items[0].AvailableQuantity.Should().Be(50);
        nextCursor.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_DifferentMarket_DoesNotLeakAcrossMarketsAsync()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        var marketAId = await CreateMarketAsync();
        var marketBId = await CreateMarketAsync();
        var unitId = await GetOrCreateUnitAsync("kg");
        var productName = $"Bắp cải {Guid.NewGuid():N}";
        var productId = await CreateProductAsync(productName, unitId);
        await SeedMarketProductAsync(marketAId, productId, 15_000m, 10);

        var repository = GetRepository();

        // Act — search market B, where the product is not listed
        var (items, _) = await repository.SearchAsync(
            new MarketProductSearchCriteria(marketBId, productName, null, false, null, 20),
            CancellationToken.None);

        // Assert
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_InStockOnlyTrue_ExcludesFullyReservedItemsAsync()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        var marketId = await CreateMarketAsync();
        var unitId = await GetOrCreateUnitAsync("kg");
        var searchTerm = $"Rau-{Guid.NewGuid():N}";
        var inStockProductId = await CreateProductAsync($"{searchTerm} muống", unitId);
        var outOfStockProductId = await CreateProductAsync($"{searchTerm} cải", unitId);
        await SeedMarketProductAsync(marketId, inStockProductId, 10_000m, 20);
        var outOfStockMpId = await SeedMarketProductAsync(marketId, outOfStockProductId, 12_000m, 5);
        await ReserveAllAsync(outOfStockMpId, 5);

        var repository = GetRepository();

        // Act
        var (items, _) = await repository.SearchAsync(
            new MarketProductSearchCriteria(marketId, searchTerm, null, InStockOnly: true, null, 20),
            CancellationToken.None);

        // Assert
        items.Should().ContainSingle();
        items[0].ProductId.Should().Be(inStockProductId);
    }

    [Fact]
    public async Task SearchAsync_PageSizeOne_CursorPaginatesThroughAllMatchesAsync()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        var marketId = await CreateMarketAsync();
        var unitId = await GetOrCreateUnitAsync("kg");
        var searchTerm = $"Cam-{Guid.NewGuid():N}";
        var product1Id = await CreateProductAsync($"{searchTerm} sành", unitId);
        var product2Id = await CreateProductAsync($"{searchTerm} vàng", unitId);
        await SeedMarketProductAsync(marketId, product1Id, 30_000m, 40);
        await SeedMarketProductAsync(marketId, product2Id, 35_000m, 25);

        var repository = GetRepository();

        // Act — page 1
        var (page1Items, page1Cursor) = await repository.SearchAsync(
            new MarketProductSearchCriteria(marketId, searchTerm, null, false, null, 1),
            CancellationToken.None);

        // Assert — page 1
        page1Items.Should().HaveCount(1);
        page1Cursor.Should().NotBeNullOrEmpty();

        // Act — page 2
        var (page2Items, page2Cursor) = await repository.SearchAsync(
            new MarketProductSearchCriteria(marketId, searchTerm, null, false, page1Cursor, 1),
            CancellationToken.None);

        // Assert — page 2
        page2Items.Should().HaveCount(1);
        page2Items[0].ProductId.Should().NotBe(page1Items[0].ProductId);
        page2Cursor.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_SoftDeletedMarketProduct_IsExcludedAsync()
    {
        // Arrange
        await AuthenticateAsAdminAsync();
        var marketId = await CreateMarketAsync();
        var unitId = await GetOrCreateUnitAsync("kg");
        var productName = $"Khoai-{Guid.NewGuid():N}";
        var productId = await CreateProductAsync(productName, unitId);
        var marketProductId = await SeedMarketProductAsync(marketId, productId, 8_000m, 100);
        await SoftDeleteMarketProductAsync(marketProductId);

        var repository = GetRepository();

        // Act
        var (items, _) = await repository.SearchAsync(
            new MarketProductSearchCriteria(marketId, productName, null, false, null, 20),
            CancellationToken.None);

        // Assert
        items.Should().BeEmpty();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IMarketProductRepository GetRepository()
    {
        var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IMarketProductRepository>();
    }

    private async Task AuthenticateAsAdminAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = "admin@test.freshflow", password = "AdminP@ss1" });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", env!.Data!.AccessToken);
    }

    private async Task<Guid> CreateMarketAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/markets", new
        {
            name = $"IT-Mkt-{Guid.NewGuid():N}",
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

    private async Task ReserveAllAsync(Guid marketProductId, int reserved)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE market_products SET "ReservedQuantity" = {reserved} WHERE "Id" = {marketProductId}
            """);
    }

    private async Task SoftDeleteMarketProductAsync(Guid marketProductId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE market_products SET "deleted_at" = now() WHERE "Id" = {marketProductId}
            """);
    }
}
