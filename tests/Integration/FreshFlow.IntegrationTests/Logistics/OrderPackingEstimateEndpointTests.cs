using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Logistics;

[Trait("Category", "Integration")]
public sealed class OrderPackingEstimateEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PackingEstimate_ExecutesPostgresSeamAndExcludesMissingCodeAsync()
    {
        await AuthenticateAsAdminAsync();
        var restaurantId = await CreateRestaurantAsync();
        var orderId = await SeedOrderAsync(restaurantId);

        var response = await _client.GetAsync(
            $"/api/v1/logistics/shipping/orders/{orderId}/estimate");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Envelope<ShipmentEstimateDto>>();
        body!.Data!.TotalBoxes.Should().Be(3);
        body.Data.TotalLoadKg.Should().Be(51m);
        body.Data.Lines.Should().ContainSingle().Which.ProductName.Should().Be("Packed fish");
        body.Data.MissingPackingCode.Should().Equal("Loose vegetables");
    }

    private async Task<Guid> SeedOrderAsync(Guid restaurantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var unit = new UnitOfMeasurement($"kg-{Guid.NewGuid():N}", "kg");
        var packingCode = new PackingCode($"BOX-{Guid.NewGuid():N}", null, 15m);
        var market = new Market(
            $"Packing Market {Guid.NewGuid():N}", "HCMC", "1 Test Street", null, null);
        db.Set<UnitOfMeasurement>().Add(unit);
        db.Set<PackingCode>().Add(packingCode);
        db.Set<Market>().Add(market);
        await db.SaveChangesAsync();

        var packedProduct = new Product(
            "Packed fish", unit.Id, null, null, null, packingCodeId: packingCode.Id);
        var missingProduct = new Product(
            "Loose vegetables", unit.Id, null, null, null);
        db.Set<Product>().AddRange(packedProduct, missingProduct);
        await db.SaveChangesAsync();

        var packedMarketProduct = new MarketProduct(
            market.Id, packedProduct.Id, 100_000m, 100, null);
        var missingMarketProduct = new MarketProduct(
            market.Id, missingProduct.Id, 20_000m, 100, null);
        db.Set<MarketProduct>().AddRange(packedMarketProduct, missingMarketProduct);
        await db.SaveChangesAsync();

        var order = new Order(restaurantId, null, null);
        order.AddItem(packedMarketProduct.Id, packedProduct.Name, 45, 100_000m)
            .IsSuccess.Should().BeTrue();
        order.AddItem(missingMarketProduct.Id, missingProduct.Name, 10, 20_000m)
            .IsSuccess.Should().BeTrue();
        order.ClearDomainEvents();
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();

        return order.Id;
    }

    private async Task AuthenticateAsAdminAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { identifier = "admin@test.freshflow", password = "AdminP@ss1" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
    }

    private async Task<Guid> CreateRestaurantAsync()
    {
        var email = $"packing-{Guid.NewGuid():N}@test.freshflow";
        var create = await _client.PostAsJsonAsync(
            "/api/v1/admin/users",
            new
            {
                email,
                password = "RestaurantP@ss1",
                role = "restaurant",
                restaurantName = "Packing Test Restaurant"
            });
        create.EnsureSuccessStatusCode();

        var response = await _client.GetAsync(
            $"/api/v1/admin/users?search={Uri.EscapeDataString(email)}&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<UserListBody>>();
        return body!.Data!.Data.Should().ContainSingle()
            .Which.RestaurantId.Should().NotBeNull().And.Subject!.Value;
    }

    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);
    private sealed record UserSummaryBody(Guid? RestaurantId);
}
