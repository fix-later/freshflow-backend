using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Pricing.Domain.Entities;
using FreshFlow.Procurement.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.IntegrationTests.Hub;

[Trait("Category", "Integration")]
public sealed class HubOrdersByRestaurantEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private static readonly DateOnly ServiceDate = new(2026, 7, 29);
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task OrdersByRestaurant_FiltersHubStatusAndVietnamDay_AndIncludesBatchedOnRequestAsync()
    {
        await AuthenticateAsAdminAsync();
        var firstRestaurantId = await CreateRestaurantAsync("Nhà hàng A");
        var secondRestaurantId = await CreateRestaurantAsync("Nhà hàng B");
        var seed = await SeedAsync(firstRestaurantId, secondRestaurantId);

        var response = await _client.GetAsync(
            $"/api/v1/hubs/{seed.HubId}/orders-by-restaurant?service_date={ServiceDate:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Envelope<HubOrdersByRestaurantDto>>();
        body!.Data!.HubId.Should().Be(seed.HubId);
        body.Data.ServiceDate.Should().Be(ServiceDate);
        body.Data.Restaurants.Should().HaveCount(2);

        var first = body.Data.Restaurants.Single(group => group.RestaurantId == firstRestaurantId);
        first.RestaurantName.Should().Be("Nhà hàng A");
        first.OrderCount.Should().Be(1);
        first.Lines.Should().ContainSingle(line =>
            line.OrderId == seed.FirstAtHubOrderId &&
            line.ProductName == "Hub fish" &&
            line.OrderedQuantity == 2 &&
            line.MarketProductId == seed.MarketProductId &&
            line.ProductId == seed.ProductId &&
            line.Unit == seed.Unit &&
            line.CapacityKg == 12m);

        var second = body.Data.Restaurants.Single(group => group.RestaurantId == secondRestaurantId);
        second.RestaurantName.Should().Be("Nhà hàng B");
        second.OrderCount.Should().Be(1);
        second.Lines.Should().ContainSingle(line =>
            line.OrderId == seed.SecondAtHubOrderId &&
            line.OrderedQuantity == 3);

        body.Data.Restaurants.SelectMany(group => group.Lines).Select(line => line.OrderId)
            .Should().NotContain([
                seed.BatchedOrderId,
                seed.DraftOrderId,
                seed.OtherVietnamDayOrderId,
                seed.OtherHubOrderId
            ]);

        var included = await _client.GetFromJsonAsync<Envelope<HubOrdersByRestaurantDto>>(
            $"/api/v1/hubs/{seed.HubId}/orders-by-restaurant" +
            $"?service_date={ServiceDate:yyyy-MM-dd}&include_batched=true");
        var includedFirst = included!.Data!.Restaurants
            .Single(group => group.RestaurantId == firstRestaurantId);
        includedFirst.OrderCount.Should().Be(2);
        includedFirst.Lines.Select(line => line.OrderId)
            .Should().BeEquivalentTo(new[] { seed.FirstAtHubOrderId, seed.BatchedOrderId });
    }

    private async Task<SeededOrders> SeedAsync(Guid firstRestaurantId, Guid secondRestaurantId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var unit = new UnitOfMeasurement($"unit-{Guid.NewGuid():N}", "unit");
        var packingCode = new PackingCode($"BOX-{Guid.NewGuid():N}", null, 12m);
        var market = new Market($"Hub Market {Guid.NewGuid():N}", "HCMC", "1 Hub St", null, null);
        var otherMarket = new Market(
            $"Other Hub Market {Guid.NewGuid():N}", "HCMC", "2 Hub St", null, null);
        db.AddRange(unit, packingCode, market, otherMarket);
        await db.SaveChangesAsync();

        var product = new Product(
            "Hub fish", unit.Id, null, null, null, packingCodeId: packingCode.Id);
        db.Add(product);
        await db.SaveChangesAsync();

        var marketProduct = new MarketProduct(market.Id, product.Id, 10_000m, 100, null);
        var otherMarketProduct = new MarketProduct(otherMarket.Id, product.Id, 10_000m, 100, null);
        var hub = HubEntity.Create("Hub A", "1 Hub St", null, null, 1000m, null, market.Id);
        var otherHub = HubEntity.Create(
            "Hub B", "2 Hub St", null, null, 1000m, null, otherMarket.Id);
        db.AddRange(marketProduct, otherMarketProduct, hub, otherHub);
        await db.SaveChangesAsync();

        var inWindowUtc = new DateTime(2026, 7, 29, 3, 0, 0, DateTimeKind.Utc);
        var nextVietnamDayUtc = new DateTime(2026, 7, 29, 22, 0, 0, DateTimeKind.Utc);
        var firstAtHub = NewOrder(
            firstRestaurantId, marketProduct.Id, inWindowUtc, 2, OrderStatus.AtHub);
        var secondAtHub = NewOrder(
            secondRestaurantId, marketProduct.Id, inWindowUtc, 3, OrderStatus.AtHub);
        var batched = NewOrder(
            firstRestaurantId, marketProduct.Id, inWindowUtc, 4, OrderStatus.Batched);
        var draft = NewOrder(
            secondRestaurantId, marketProduct.Id, inWindowUtc, 5, OrderStatus.Draft);
        var otherVietnamDay = NewOrder(
            secondRestaurantId, marketProduct.Id, nextVietnamDayUtc, 6, OrderStatus.AtHub);
        var otherHubOrder = NewOrder(
            secondRestaurantId, otherMarketProduct.Id, inWindowUtc, 7, OrderStatus.AtHub);
        var orders = new[]
        {
            firstAtHub,
            secondAtHub,
            batched,
            draft,
            otherVietnamDay,
            otherHubOrder
        };
        db.Set<Order>().AddRange(orders.Select(order => order.Entity));
        foreach (var order in orders)
            db.Entry(order.Entity).Property(nameof(Order.Status)).CurrentValue = order.SeededStatus;
        await db.SaveChangesAsync();

        var batch = ProcurementBatch.Build(
            ServiceDate,
            market.Id,
            orders[..5].Select(order =>
                (marketProduct.Id, "Hub fish", order.Quantity, order.Entity.Id)),
            hub.Id).Value;
        var otherBatch = ProcurementBatch.Build(
            ServiceDate,
            otherMarket.Id,
            [(otherMarketProduct.Id, "Hub fish", otherHubOrder.Quantity, otherHubOrder.Entity.Id)],
            otherHub.Id).Value;
        batch.ClearDomainEvents();
        otherBatch.ClearDomainEvents();
        db.AddRange(batch, otherBatch);
        await db.SaveChangesAsync();

        return new SeededOrders(
            hub.Id,
            marketProduct.Id,
            product.Id,
            unit.Name,
            firstAtHub.Entity.Id,
            secondAtHub.Entity.Id,
            batched.Entity.Id,
            draft.Entity.Id,
            otherVietnamDay.Entity.Id,
            otherHubOrder.Entity.Id);
    }

    private static SeededOrder NewOrder(
        Guid restaurantId,
        Guid marketProductId,
        DateTime scheduledFor,
        int quantity,
        OrderStatus status)
    {
        var order = new Order(restaurantId, scheduledFor, null);
        order.AddItem(marketProductId, "Hub fish", quantity, 10_000m)
            .IsSuccess.Should().BeTrue();
        order.ClearDomainEvents();
        return new SeededOrder(order, status, quantity);
    }

    private async Task<Guid> CreateRestaurantAsync(string name)
    {
        var email = $"hub-orders-{Guid.NewGuid():N}@test.freshflow";
        var create = await _client.PostAsJsonAsync(
            "/api/v1/admin/users",
            new
            {
                email,
                password = "RestaurantP@ss1",
                role = "restaurant",
                restaurantName = name
            });
        create.EnsureSuccessStatusCode();

        var response = await _client.GetAsync(
            $"/api/v1/admin/users?search={Uri.EscapeDataString(email)}&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<UserListBody>>();
        return body!.Data!.Data.Should().ContainSingle()
            .Which.RestaurantId.Should().NotBeNull().And.Subject!.Value;
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

    private sealed record SeededOrder(Order Entity, OrderStatus SeededStatus, int Quantity);

    private sealed record SeededOrders(
        Guid HubId,
        Guid MarketProductId,
        Guid ProductId,
        string Unit,
        Guid FirstAtHubOrderId,
        Guid SecondAtHubOrderId,
        Guid BatchedOrderId,
        Guid DraftOrderId,
        Guid OtherVietnamDayOrderId,
        Guid OtherHubOrderId);

    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);

    private sealed record UserSummaryBody(Guid? RestaurantId);
}
