using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Orders;

/// <summary>
/// SCRUM-368 — GET/POST/DELETE /api/v1/restaurants/me/favorites. The GET seam is a
/// <c>ToSqlQuery</c> join across restaurant_favorites/market_products/products/markets, which
/// EF's InMemory provider never executes — only proven here, against real Postgres.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RestaurantFavoritesEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Favorite_ThenGet_ReturnsEnrichedItemAsync()
    {
        var restaurantToken = await CreateAndLoginRestaurantAsync();
        var product = await SeedMarketProductAsync();

        var add = await _client.PostAsJsonAsync(
            "/api/v1/restaurants/me/favorites",
            new { marketProductId = product.MarketProductId });
        add.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await _client.GetAsync("/api/v1/restaurants/me/favorites");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await get.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<FavoriteItemDto>>>();

        body!.Data.Should().ContainSingle(item => item.MarketProductId == product.MarketProductId)
            .Which.Should().BeEquivalentTo(new
            {
                ProductName = product.ProductName,
                MarketName = product.MarketName,
                Unit = product.UnitName,
                CurrentPrice = product.CurrentPrice,
                AvailableQuantity = product.CurrentQuantity - product.ReservedQuantity
            }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Unfavorite_RemovesFromGetAsync()
    {
        var restaurantToken = await CreateAndLoginRestaurantAsync();
        var product = await SeedMarketProductAsync();
        await _client.PostAsJsonAsync(
            "/api/v1/restaurants/me/favorites",
            new { marketProductId = product.MarketProductId });

        var remove = await _client.DeleteAsync(
            $"/api/v1/restaurants/me/favorites/{product.MarketProductId}");
        remove.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await _client.GetAsync("/api/v1/restaurants/me/favorites");
        var body = await get.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<FavoriteItemDto>>>();
        body!.Data.Should().NotContain(item => item.MarketProductId == product.MarketProductId);
    }

    [Fact]
    public async Task Unfavorite_NotFavorited_StillReturns204Async()
    {
        await CreateAndLoginRestaurantAsync();

        var remove = await _client.DeleteAsync(
            $"/api/v1/restaurants/me/favorites/{Guid.NewGuid()}");

        remove.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Favorite_SameProductTwice_KeepsExactlyOneRowAsync()
    {
        await CreateAndLoginRestaurantAsync();
        var product = await SeedMarketProductAsync();

        var first = await _client.PostAsJsonAsync(
            "/api/v1/restaurants/me/favorites",
            new { marketProductId = product.MarketProductId });
        var second = await _client.PostAsJsonAsync(
            "/api/v1/restaurants/me/favorites",
            new { marketProductId = product.MarketProductId });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await _client.GetAsync("/api/v1/restaurants/me/favorites");
        var body = await get.Content.ReadFromJsonAsync<Envelope<IReadOnlyList<FavoriteItemDto>>>();
        body!.Data.Should().ContainSingle(item => item.MarketProductId == product.MarketProductId);
    }

    [Fact]
    public async Task Favorite_UnknownMarketProductId_Returns404Async()
    {
        await CreateAndLoginRestaurantAsync();

        var add = await _client.PostAsJsonAsync(
            "/api/v1/restaurants/me/favorites",
            new { marketProductId = Guid.NewGuid() });

        add.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await add.Content.ReadFromJsonAsync<ErrorEnvelope>();
        body!.Error!.Code.Should().Be("MARKET_PRODUCT_NOT_FOUND");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> CreateAndLoginRestaurantAsync()
    {
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var email = $"favorites-{Guid.NewGuid():N}@test.freshflow";
        const string password = "RestaurantP@ss1";
        var create = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password,
            role = "restaurant",
            restaurantName = $"Favorites Test Restaurant {Guid.NewGuid():N}"
        });
        create.EnsureSuccessStatusCode();

        var restaurantToken = await LoginAsync(email, password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", restaurantToken);
        return restaurantToken;
    }

    private async Task<string> LoginAsync(string identifier, string password)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { identifier, password });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        return env!.Data!.AccessToken;
    }

    private async Task<SeededMarketProduct> SeedMarketProductAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var unit = new UnitOfMeasurement($"kg-{Guid.NewGuid():N}", "kg");
        var market = new Market($"Favorites Market {Guid.NewGuid():N}", "HCMC", "1 Test Street", null, null);
        db.Set<UnitOfMeasurement>().Add(unit);
        db.Set<Market>().Add(market);
        await db.SaveChangesAsync();

        var product = new Product("Favorites Tomato", unit.Id, null, null, null);
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync();

        var marketProduct = new MarketProduct(market.Id, product.Id, 12_500m, 40, null);
        db.Set<MarketProduct>().Add(marketProduct);
        await db.SaveChangesAsync();

        return new SeededMarketProduct(
            marketProduct.Id,
            product.Name,
            market.Name,
            unit.Name,
            marketProduct.CurrentPrice,
            marketProduct.CurrentQuantity,
            marketProduct.ReservedQuantity);
    }

    private sealed record SeededMarketProduct(
        Guid MarketProductId,
        string ProductName,
        string MarketName,
        string UnitName,
        decimal CurrentPrice,
        int CurrentQuantity,
        int ReservedQuantity);
}
