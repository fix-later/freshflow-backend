using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Logistics;

/// <summary>
/// SCRUM-369 — GET /api/v1/logistics/routes/{id}/loading-manifest. The manifest runs two
/// <c>ToSqlQuery</c> keyless seams that EF InMemory never executes — <c>OrderStatusRow</c>
/// (ListByRestaurantsAndStatusAsync) and <c>OrderPackingLineRow</c> (GetLinesByOrdersAsync) — so
/// the Contains()/status filtering is only proven here, against real Postgres.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RouteLoadingManifestEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task LoadingManifest_ExecutesPostgresSeams_ReturnsAtHubGoodsPerStopAsync()
    {
        await AuthenticateAsAdminAsync();
        var restaurantId = await CreateRestaurantAsync();
        var seed = await SeedAtHubAndDraftOrdersAsync(restaurantId);
        var routeId = await SeedRouteAsync(seed.MarketId, restaurantId);

        var response = await _client.GetAsync(
            $"/api/v1/logistics/routes/{routeId}/loading-manifest");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Envelope<LoadingManifestDto>>();

        var stop = body!.Data!.Stops.Should().ContainSingle()
            .Which;
        stop.RestaurantId.Should().Be(restaurantId);
        stop.RestaurantName.Should().Be("Manifest Restaurant");
        // AtHub order shows up with its packing line (proves GetLinesByOrdersAsync on Postgres).
        var line = stop.Lines.Should().ContainSingle(line =>
            line.ProductName == "Packed fish" && line.Quantity == 8 && line.CapacityKg == 15m)
            .Which;
        // orderId/orderItemId sourced from order_items."Id" via the seam (proves the extended
        // ToSqlQuery projection on Postgres).
        line.OrderId.Should().Be(seed.AtHubOrderId);
        line.OrderItemId.Should().NotBeEmpty();
        // The Draft order at the SAME restaurant must be filtered out
        // (proves ListByRestaurantsAndStatusAsync's status filter on Postgres).
        stop.Lines.Should().NotContain(line => line.ProductName == "Draft-only greens");
    }

    private async Task<SeededGoods> SeedAtHubAndDraftOrdersAsync(Guid restaurantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var unit = new UnitOfMeasurement($"kg-{Guid.NewGuid():N}", "kg");
        var packingCode = new PackingCode($"BOX-{Guid.NewGuid():N}", null, 15m);
        var market = new Market(
            $"Manifest Market {Guid.NewGuid():N}", "HCMC", "1 Test Street", null, null);
        db.Set<UnitOfMeasurement>().Add(unit);
        db.Set<PackingCode>().Add(packingCode);
        db.Set<Market>().Add(market);
        await db.SaveChangesAsync();

        var packedProduct = new Product(
            "Packed fish", unit.Id, null, null, null, packingCodeId: packingCode.Id);
        var greensProduct = new Product("Draft-only greens", unit.Id, null, null, null);
        db.Set<Product>().AddRange(packedProduct, greensProduct);
        await db.SaveChangesAsync();

        var packedMarketProduct = new MarketProduct(market.Id, packedProduct.Id, 100_000m, 100, null);
        var greensMarketProduct = new MarketProduct(market.Id, greensProduct.Id, 20_000m, 100, null);
        db.Set<MarketProduct>().AddRange(packedMarketProduct, greensMarketProduct);
        await db.SaveChangesAsync();

        var atHubOrder = new Order(restaurantId, null, null);
        atHubOrder.AddItem(packedMarketProduct.Id, packedProduct.Name, 8, 100_000m)
            .IsSuccess.Should().BeTrue();
        atHubOrder.ClearDomainEvents();

        var draftOrder = new Order(restaurantId, null, null);
        draftOrder.AddItem(greensMarketProduct.Id, greensProduct.Name, 3, 20_000m)
            .IsSuccess.Should().BeTrue();
        draftOrder.ClearDomainEvents();

        db.Set<Order>().Add(atHubOrder);
        db.Entry(atHubOrder).Property(nameof(Order.Status)).CurrentValue = OrderStatus.AtHub;
        db.Set<Order>().Add(draftOrder); // stays Draft -> excluded from the manifest
        await db.SaveChangesAsync();

        return new SeededGoods(market.Id, atHubOrder.Id);
    }

    private async Task<Guid> SeedRouteAsync(Guid marketId, Guid restaurantId)
    {
        IReadOnlyList<RouteStop> stops =
        [
            new(0, StopEntityType.market, marketId, "Manifest Market", 10.75m, 106.67m, null, null),
            new(1, StopEntityType.restaurant, restaurantId, "Manifest Restaurant", 10.76m, 106.68m, null, null),
        ];
        var route = DeliveryRoute.CreateDirect(
            DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)), stops, null);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Set<DeliveryRoute>().Add(route);
        await db.SaveChangesAsync();

        return route.Id;
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
        var email = $"manifest-{Guid.NewGuid():N}@test.freshflow";
        var create = await _client.PostAsJsonAsync(
            "/api/v1/admin/users",
            new
            {
                email,
                password = "RestaurantP@ss1",
                role = "restaurant",
                restaurantName = "Manifest Test Restaurant"
            });
        create.EnsureSuccessStatusCode();

        var response = await _client.GetAsync(
            $"/api/v1/admin/users?search={Uri.EscapeDataString(email)}&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<UserListBody>>();
        return body!.Data!.Data.Should().ContainSingle()
            .Which.RestaurantId.Should().NotBeNull().And.Subject!.Value;
    }

    private sealed record SeededGoods(Guid MarketId, Guid AtHubOrderId);

    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);

    private sealed record UserSummaryBody(Guid? RestaurantId);
}
