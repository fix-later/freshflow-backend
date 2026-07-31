using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Contracts;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Pricing.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Orders;

[Trait("Category", "Integration")]
public sealed class AtomicStockReservationEndpointTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    [Fact]
    public async Task Confirm_ConcurrentOrdersForLastStock_ExactlyOneSucceedsAsync()
    {
        var restaurant = await CreateRestaurantAsync();
        var seeded = await SeedOrdersAsync(restaurant.RestaurantId, stock: 1, quantities: [1, 1]);
        using var firstClient = Client(restaurant.Token);
        using var secondClient = Client(restaurant.Token);

        var responses = await Task.WhenAll(
            firstClient.PostAsync($"/api/v1/orders/{seeded.OrderIds[0]}/confirm", null),
            secondClient.PostAsync($"/api/v1/orders/{seeded.OrderIds[1]}/confirm", null));

        responses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Set<MarketProduct>().AsNoTracking()
            .SingleAsync(value => value.Id == seeded.MarketProductIds[0]);
        var account = await db.Set<RestaurantCredit>().AsNoTracking()
            .SingleAsync(value => value.RestaurantId == restaurant.RestaurantId);
        var confirmed = await db.Set<Order>().AsNoTracking()
            .CountAsync(order => seeded.OrderIds.Contains(order.Id) && order.Status == OrderStatus.Confirmed);

        product.ReservedQuantity.Should().Be(1);
        account.OutstandingBalance.Should().Be(10_000m);
        confirmed.Should().Be(1);
    }

    [Fact]
    public async Task Confirm_ConcurrentRequestsForSameOrder_ChargesAndReservesOnceAsync()
    {
        var restaurant = await CreateRestaurantAsync();
        var seeded = await SeedOrdersAsync(restaurant.RestaurantId, stock: 2, quantities: [1]);
        using var firstClient = Client(restaurant.Token);
        using var secondClient = Client(restaurant.Token);
        var endpoint = $"/api/v1/orders/{seeded.OrderIds[0]}/confirm";

        var responses = await Task.WhenAll(
            firstClient.PostAsync(endpoint, null),
            secondClient.PostAsync(endpoint, null));

        responses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Set<MarketProduct>().AsNoTracking()
            .SingleAsync(value => value.Id == seeded.MarketProductIds[0]);
        var account = await db.Set<RestaurantCredit>().AsNoTracking()
            .SingleAsync(value => value.RestaurantId == restaurant.RestaurantId);
        var charges = await db.Set<CreditTransaction>().AsNoTracking()
            .CountAsync(value => value.OrderId == seeded.OrderIds[0] && value.Type == CreditTransactionType.Charge);

        product.ReservedQuantity.Should().Be(1);
        account.OutstandingBalance.Should().Be(10_000m);
        charges.Should().Be(1);
    }

    [Fact]
    public async Task Confirm_OneInsufficientLine_RollsBackEveryLineAsync()
    {
        var restaurant = await CreateRestaurantAsync();
        var seeded = await SeedMultiLineOrderAsync(restaurant.RestaurantId);
        using var client = Client(restaurant.Token);

        var response = await client.PostAsync($"/api/v1/orders/{seeded.OrderId}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var error = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        error!.Error!.Code.Should().Be("INSUFFICIENT_STOCK");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var products = await db.Set<MarketProduct>().AsNoTracking()
            .Where(value => seeded.MarketProductIds.Contains(value.Id))
            .ToListAsync();
        var order = await db.Set<Order>().AsNoTracking().SingleAsync(value => value.Id == seeded.OrderId);
        var account = await db.Set<RestaurantCredit>().AsNoTracking()
            .SingleAsync(value => value.RestaurantId == restaurant.RestaurantId);

        products.Should().OnlyContain(value => value.ReservedQuantity == 0);
        order.Status.Should().Be(OrderStatus.Draft);
        account.OutstandingBalance.Should().Be(0m);
    }

    [Fact]
    public async Task Confirm_CreditRejected_DoesNotReserveOrConfirmAsync()
    {
        var restaurant = await CreateRestaurantAsync();
        var seeded = await SeedOrdersAsync(
            restaurant.RestaurantId, stock: 5, quantities: [1], creditLimit: 0m);
        using var client = Client(restaurant.Token);

        var response = await client.PostAsync($"/api/v1/orders/{seeded.OrderIds[0]}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Set<MarketProduct>().AsNoTracking()
            .SingleAsync(value => value.Id == seeded.MarketProductIds[0]);
        var order = await db.Set<Order>().AsNoTracking()
            .SingleAsync(value => value.Id == seeded.OrderIds[0]);

        product.ReservedQuantity.Should().Be(0);
        order.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public async Task Confirm_FailureAfterReservation_RollsBackStockAndOrderAsync()
    {
        var restaurant = await CreateRestaurantAsync();
        var seeded = await SeedOrdersAsync(restaurant.RestaurantId, stock: 5, quantities: [1]);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var result = await repository.ExecuteInSerializableTransactionAsync(async ct =>
            {
                var order = await repository.FindByIdAsync(seeded.OrderIds[0], ct);
                (await repository.TryReserveStockAsync(
                    [new StockReservation(seeded.MarketProductIds[0], 1)], ct)).Should().BeTrue();
                order!.Confirm();
                repository.Track(order);
                return Result.Failure(Error.Conflict("CREDIT_CHARGE_FAILED", "simulated"));
            }, default);

            result.IsFailure.Should().BeTrue();
        }

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Set<MarketProduct>().AsNoTracking()
            .SingleAsync(value => value.Id == seeded.MarketProductIds[0]);
        var order = await db.Set<Order>().AsNoTracking()
            .SingleAsync(value => value.Id == seeded.OrderIds[0]);

        product.ReservedQuantity.Should().Be(0);
        order.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public async Task Cancel_ConfirmedOrder_ReleasesStockAndCreditOnceAsync()
    {
        var restaurant = await CreateRestaurantAsync();
        var seeded = await SeedOrdersAsync(restaurant.RestaurantId, stock: 5, quantities: [2]);
        using var client = Client(restaurant.Token);
        var orderId = seeded.OrderIds[0];
        (await client.PostAsync($"/api/v1/orders/{orderId}/confirm", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var first = await client.PatchAsJsonAsync($"/api/v1/orders/{orderId}/cancel", new { reason = "test" });
        var second = await client.PatchAsJsonAsync($"/api/v1/orders/{orderId}/cancel", new { reason = "duplicate" });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Set<MarketProduct>().AsNoTracking()
            .SingleAsync(value => value.Id == seeded.MarketProductIds[0]);
        var account = await db.Set<RestaurantCredit>().AsNoTracking()
            .SingleAsync(value => value.RestaurantId == restaurant.RestaurantId);
        var transactions = await db.Set<CreditTransaction>().AsNoTracking()
            .Where(value => value.OrderId == orderId)
            .ToListAsync();

        product.CurrentQuantity.Should().Be(5);
        product.ReservedQuantity.Should().Be(0);
        account.OutstandingBalance.Should().Be(0m);
        transactions.Count(value => value.Type == CreditTransactionType.Charge).Should().Be(1);
        transactions.Count(value => value.Type == CreditTransactionType.Refund).Should().Be(1);
    }

    [Fact]
    public async Task Handover_RepeatedEvent_ConsumesReservationOnceAsync()
    {
        var restaurant = await CreateRestaurantAsync();
        var seeded = await SeedBatchedOrderAsync(restaurant.RestaurantId);
        var integrationEvent = new ProcurementBatchHandedOffIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            [seeded.OrderId]);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(integrationEvent);
            await publisher.Publish(integrationEvent);
        }

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Set<MarketProduct>().AsNoTracking()
            .SingleAsync(value => value.Id == seeded.MarketProductId);
        var order = await db.Set<Order>().AsNoTracking()
            .SingleAsync(value => value.Id == seeded.OrderId);

        product.CurrentQuantity.Should().Be(3);
        product.ReservedQuantity.Should().Be(0);
        order.Status.Should().Be(OrderStatus.AtHub);
    }

    private HttpClient Client(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<RestaurantIdentity> CreateRestaurantAsync()
    {
        using var client = factory.CreateClient();
        var adminToken = await LoginAsync(client, "admin@test.freshflow", "AdminP@ss1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"stock-{Guid.NewGuid():N}@test.freshflow";
        const string password = "RestaurantP@ss1";
        (await client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password,
            role = "restaurant",
            restaurantName = $"Stock Test {Guid.NewGuid():N}"
        })).EnsureSuccessStatusCode();

        var users = await client.GetFromJsonAsync<Envelope<UserListBody>>(
            $"/api/v1/admin/users?search={Uri.EscapeDataString(email)}&page=1&pageSize=10");
        var restaurantId = users!.Data!.Data.Single().RestaurantId!.Value;
        (await client.PatchAsync($"/api/v1/admin/restaurants/{restaurantId}/approve", null))
            .EnsureSuccessStatusCode();

        return new RestaurantIdentity(
            restaurantId,
            await LoginAsync(client, email, password));
    }

    private static async Task<string> LoginAsync(HttpClient client, string identifier, string password)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { identifier, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>())!.Data!.AccessToken;
    }

    private async Task<SeededOrders> SeedOrdersAsync(
        Guid restaurantId,
        int stock,
        IReadOnlyList<int> quantities,
        decimal creditLimit = 1_000_000m)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var marketProduct = await AddMarketProductAsync(db, stock);
        var orders = quantities.Select(quantity =>
        {
            var order = new Order(restaurantId, null, null);
            order.AddItem(marketProduct.Id, "Cà chua", quantity, marketProduct.CurrentPrice);
            order.ClearDomainEvents();
            return order;
        }).ToArray();

        db.Set<Order>().AddRange(orders);
        db.Set<RestaurantCredit>().Add(new RestaurantCredit(restaurantId, creditLimit));
        await db.SaveChangesAsync();
        return new SeededOrders([marketProduct.Id], orders.Select(order => order.Id).ToArray());
    }

    private async Task<SeededMultiLineOrder> SeedMultiLineOrderAsync(Guid restaurantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = await AddMarketProductAsync(db, 5);
        var second = await AddMarketProductAsync(db, 5);
        var sufficient = first.Id.CompareTo(second.Id) < 0 ? first : second;
        var insufficient = sufficient == first ? second : first;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE market_products SET \"CurrentQuantity\" = 0 WHERE \"Id\" = {insufficient.Id}");

        var order = new Order(restaurantId, null, null);
        order.AddItem(sufficient.Id, "Sufficient", 2, sufficient.CurrentPrice);
        order.AddItem(insufficient.Id, "Insufficient", 1, insufficient.CurrentPrice);
        order.ClearDomainEvents();
        db.Set<Order>().Add(order);
        db.Set<RestaurantCredit>().Add(new RestaurantCredit(restaurantId, 1_000_000m));
        await db.SaveChangesAsync();
        return new SeededMultiLineOrder([sufficient.Id, insufficient.Id], order.Id);
    }

    private async Task<SeededBatchedOrder> SeedBatchedOrderAsync(Guid restaurantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var marketProduct = await AddMarketProductAsync(db, 5);
        var order = new Order(restaurantId, null, null);
        order.AddItem(marketProduct.Id, "Cà chua", 2, marketProduct.CurrentPrice);
        order.Confirm();
        order.AdvanceStatus(OrderStatus.Batched);
        order.ClearDomainEvents();
        db.Set<Order>().Add(order);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE market_products SET \"ReservedQuantity\" = 2 WHERE \"Id\" = {marketProduct.Id}");
        return new SeededBatchedOrder(marketProduct.Id, order.Id);
    }

    private static async Task<MarketProduct> AddMarketProductAsync(AppDbContext db, int stock)
    {
        var unit = new UnitOfMeasurement($"kg-{Guid.NewGuid():N}", "kg");
        var market = new Market($"Stock Market {Guid.NewGuid():N}", "HCMC", "1 Test Street", null, null);
        db.Set<UnitOfMeasurement>().Add(unit);
        db.Set<Market>().Add(market);
        await db.SaveChangesAsync();

        var product = new Product($"Stock Product {Guid.NewGuid():N}", unit.Id, null, null, null);
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync();

        var marketProduct = new MarketProduct(market.Id, product.Id, 10_000m, stock, null);
        db.Set<MarketProduct>().Add(marketProduct);
        await db.SaveChangesAsync();
        return marketProduct;
    }

    private sealed record RestaurantIdentity(Guid RestaurantId, string Token);
    private sealed record SeededOrders(IReadOnlyList<Guid> MarketProductIds, IReadOnlyList<Guid> OrderIds);
    private sealed record SeededMultiLineOrder(IReadOnlyList<Guid> MarketProductIds, Guid OrderId);
    private sealed record SeededBatchedOrder(Guid MarketProductId, Guid OrderId);
    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);
    private sealed record UserSummaryBody(Guid? RestaurantId);
}
