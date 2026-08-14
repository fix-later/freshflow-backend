using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Orders;

// The image seam is a keyless ToSqlQuery join (market_products -> products); ToSqlQuery does not
// run on the EF InMemory provider, so it is only actually proven against real Postgres here.
[Trait("Category", "Integration")]
public sealed class MarketProductImageSeamTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetOrder_ReturnsProductImageAndPackingCodeAsync()
    {
        var adminToken = await LoginAsync("admin@test.freshflow", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);
        var restaurantId = await CreateRestaurantAsync();
        var seed = await SeedOrderAsync(restaurantId);

        var response = await _client.GetAsync($"/api/v1/orders/{seed.OrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Envelope<OrderDto>>();
        var item = body!.Data!.Items.Should().ContainSingle().Which;
        item.ImageUrl.Should().Be("https://img/tomato.jpg");
        item.PackingCode.Should().Be(seed.PackingCode);

        using var scope = factory.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IMarketProductReader>();
        var snapshot = await reader.FindAsync(seed.MarketProductId, default);
        snapshot!.PackingCode.Should().Be(seed.PackingCode);
    }

    private async Task<string> LoginAsync(string identifier, string password)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { identifier, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        return body!.Data!.AccessToken;
    }

    private async Task<Guid> CreateRestaurantAsync()
    {
        var email = $"order-image-{Guid.NewGuid():N}@test.freshflow";
        var create = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password = "RestaurantP@ss1",
            role = "restaurant",
            restaurantName = $"Order Image Restaurant {Guid.NewGuid():N}"
        });
        create.EnsureSuccessStatusCode();

        var response = await _client.GetAsync(
            $"/api/v1/admin/users?search={Uri.EscapeDataString(email)}&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<UserListBody>>();
        return body!.Data!.Data.Should().ContainSingle()
            .Which.RestaurantId.Should().NotBeNull().And.Subject!.Value;
    }

    private async Task<SeededOrder> SeedOrderAsync(Guid restaurantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var unit = new UnitOfMeasurement($"kg-{Guid.NewGuid():N}", "kg");
        var packingCode = new PackingCode($"BOX-{Guid.NewGuid():N}", null, 15m);
        var market = new Market(
            $"Order Image Market {Guid.NewGuid():N}",
            "HCMC",
            "1 Test Street",
            null,
            null);
        var product = new Product(
            "Order Image Tomato", unit.Id, null, null, null, packingCodeId: packingCode.Id);
        product.Update(
            "Order Image Tomato", null, unit.Id, null,
            imageUrl: "https://img/tomato.jpg", packingCodeId: packingCode.Id);
        db.Set<UnitOfMeasurement>().Add(unit);
        db.Set<PackingCode>().Add(packingCode);
        db.Set<Market>().Add(market);
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync();

        var marketProduct = new MarketProduct(market.Id, product.Id, 10_000m, 100, null);
        db.Set<MarketProduct>().Add(marketProduct);
        await db.SaveChangesAsync();

        var order = new Order(restaurantId, null, null);
        order.AddItem(
            marketProduct.Id, product.Name, 1, marketProduct.CurrentPrice,
            packingCodeSnapshot: packingCode.Code);
        order.ClearDomainEvents();
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();

        return new SeededOrder(order.Id, marketProduct.Id, packingCode.Code);
    }

    private sealed record SeededOrder(Guid OrderId, Guid MarketProductId, string PackingCode);
    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);
    private sealed record UserSummaryBody(Guid? RestaurantId);
}
