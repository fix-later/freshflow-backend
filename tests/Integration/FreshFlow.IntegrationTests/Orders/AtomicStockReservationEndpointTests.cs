using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Contracts;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
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
            ConfirmAsync(firstClient, seeded.OrderIds[0], restaurant.DeliveryAddressId),
            ConfirmAsync(secondClient, seeded.OrderIds[1], restaurant.DeliveryAddressId));

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
            firstClient.PostAsJsonAsync(endpoint, new { restaurant.DeliveryAddressId }),
            secondClient.PostAsJsonAsync(endpoint, new { restaurant.DeliveryAddressId }));

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

        var response = await ConfirmAsync(client, seeded.OrderId, restaurant.DeliveryAddressId);

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

        var response = await ConfirmAsync(
            client, seeded.OrderIds[0], restaurant.DeliveryAddressId);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Set<MarketProduct>().AsNoTracking()
            .SingleAsync(value => value.Id == seeded.MarketProductIds[0]);
        var order = await db.Set<Order>().AsNoTracking()
            .SingleAsync(value => value.Id == seeded.OrderIds[0]);

        product.ReservedQuantity.Should().Be(0);
        order.Status.Should().Be(OrderStatus.Draft);
        order.DeliveryAddressId.Should().BeNull();
    }

    [Fact]
    public async Task Confirm_PartialHubCoordinates_FallsBackToMarketCoordinatesAsync()
    {
        var restaurant = await CreateRestaurantAsync();
        var seeded = await SeedOrdersAsync(restaurant.RestaurantId, stock: 1, quantities: [1]);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var marketProduct = await db.Set<MarketProduct>()
                .SingleAsync(value => value.Id == seeded.MarketProductIds[0]);
            db.Set<FreshFlow.Hub.Domain.Entities.Hub>().Add(
                FreshFlow.Hub.Domain.Entities.Hub.Create(
                    "Partial-coordinate hub", null, 11m, null, 1_000m, null, marketProduct.MarketId));
            await db.SaveChangesAsync();
        }

        using var client = Client(restaurant.Token);
        var response = await ConfirmAsync(client, seeded.OrderIds[0], restaurant.DeliveryAddressId);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Envelope<OrderDto>>();

        body!.Data!.DeliveryDistanceKm.Should().Be(0m);
        body.Data.DeliveryFee.Should().Be(0m);
        body.Data.TotalAmount.Should().Be(10_000m);
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
                order!.CaptureDeliveryAddress(
                    restaurant.DeliveryAddressId,
                    "Original Recipient",
                    "0901234567",
                    "1 Original Street",
                    10.123456m,
                    106.123456m);
                order.Confirm();
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
        order.DeliveryAddressId.Should().BeNull();
    }

    [Fact]
    public async Task Cancel_ConfirmedOrder_ReleasesStockAndCreditOnceAsync()
    {
        var restaurant = await CreateRestaurantAsync();
        var seeded = await SeedOrdersAsync(restaurant.RestaurantId, stock: 5, quantities: [2]);
        using var client = Client(restaurant.Token);
        var orderId = seeded.OrderIds[0];
        (await ConfirmAsync(client, orderId, restaurant.DeliveryAddressId))
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

    [Fact]
    public async Task Handover_Shortage_AllocatesProRataAndReleasesRemainderAsync()
    {
        var restaurant = await CreateRestaurantAsync();
        var seeded = await SeedBatchedOrdersAsync(
            restaurant.RestaurantId,
            stock: 10,
            quantities: [1, 3]);
        var integrationEvent = new ProcurementBatchHandedOffIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            seeded.OrderIds,
            PurchaseActuals:
            [
                new ProcurementPurchaseActual(seeded.MarketProductId, 2, 12_000m)
            ]);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(integrationEvent);
        }

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Set<MarketProduct>().AsNoTracking()
            .SingleAsync(value => value.Id == seeded.MarketProductId);
        var orders = (await db.Set<Order>()
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => seeded.OrderIds.Contains(order.Id))
            .ToListAsync())
            .OrderBy(order => order.Items.Single().Quantity)
            .ToList();

        product.CurrentQuantity.Should().Be(8);
        product.ReservedQuantity.Should().Be(0);
        orders.Should().OnlyContain(order => order.Status == OrderStatus.AtHub);
        orders[0].Items.Single().ActualQuantity.Should().Be(0.5m);
        orders[1].Items.Single().ActualQuantity.Should().Be(1.5m);
        orders.SelectMany(order => order.Items).Sum(item => item.ActualQuantity)
            .Should().Be(2m);
        orders.SelectMany(order => order.Items)
            .Should().OnlyContain(item => item.ActualUnitPrice == 12_000m);

        using var client = Client(restaurant.Token);
        var detail = await client.GetFromJsonAsync<Envelope<OrderDto>>(
            $"/api/v1/orders/{orders[0].Id}");
        detail!.Data!.Items.Single().ActualUnitPrice.Should().Be(12_000m);
    }

    [Fact]
    public async Task Handover_ReleaseFailure_RollsBackConsumptionAndOrderActualsAsync()
    {
        var restaurant = await CreateRestaurantAsync();
        var seeded = await SeedBatchedOrdersAsync(
            restaurant.RestaurantId,
            stock: 10,
            quantities: [1, 3],
            reservedQuantity: 3);
        var integrationEvent = new ProcurementBatchHandedOffIntegrationEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            seeded.OrderIds,
            PurchaseActuals:
            [
                new ProcurementPurchaseActual(seeded.MarketProductId, 2, 12_000m)
            ]);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            var act = () => publisher.Publish(integrationEvent);
            await act.Should().ThrowAsync<ProcurementHandoverRejectedException>()
                .Where(exception => exception.Code == "STOCK_RESERVATION_CONFLICT");
        }

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Set<MarketProduct>().AsNoTracking()
            .SingleAsync(value => value.Id == seeded.MarketProductId);
        var orders = await db.Set<Order>()
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => seeded.OrderIds.Contains(order.Id))
            .ToListAsync();

        product.CurrentQuantity.Should().Be(10);
        product.ReservedQuantity.Should().Be(3);
        orders.Should().OnlyContain(order => order.Status == OrderStatus.Batched);
        orders.SelectMany(order => order.Items)
            .Should().OnlyContain(item =>
                item.ActualQuantity == null &&
                item.ActualUnitPrice == null);
    }

    [Fact]
    public async Task Confirm_AddressUpdatedAfterwards_OrderKeepsOriginalSnapshotAsync()
    {
        var restaurant = await CreateRestaurantAsync();
        var seeded = await SeedOrdersAsync(restaurant.RestaurantId, stock: 2, quantities: [1]);
        using var client = Client(restaurant.Token);
        var orderId = seeded.OrderIds[0];

        (await ConfirmAsync(client, orderId, restaurant.DeliveryAddressId))
            .EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync(
            $"/api/v1/restaurants/me/delivery-addresses/{restaurant.DeliveryAddressId}",
            new
            {
                addressLine = "2 Changed Street",
                recipientName = "Changed Recipient",
                phone = "0909999999",
                latitude = 11.111111m,
                longitude = 107.111111m,
                isDefault = false
            })).EnsureSuccessStatusCode();

        var body = await client.GetFromJsonAsync<Envelope<OrderDto>>(
            $"/api/v1/orders/{orderId}");

        body!.Data!.DeliveryAddress.Should().BeEquivalentTo(
            new DeliveryAddressSnapshotDto(
                restaurant.DeliveryAddressId,
                "Original Recipient",
                "0901234567",
                "1 Original Street",
                10.123456m,
                106.123456m));
    }

    [Fact]
    public async Task Confirm_InvalidDeliveryAddresses_ReturnNotFoundAsync()
    {
        var restaurant = await CreateRestaurantAsync();
        var otherRestaurant = await CreateRestaurantAsync();
        var seeded = await SeedOrdersAsync(
            restaurant.RestaurantId, stock: 3, quantities: [1, 1, 1]);
        using var client = Client(restaurant.Token);

        (await client.DeleteAsync(
            $"/api/v1/restaurants/me/delivery-addresses/{restaurant.DeliveryAddressId}"))
            .EnsureSuccessStatusCode();

        var responses = new[]
        {
            await ConfirmAsync(client, seeded.OrderIds[0], restaurant.DeliveryAddressId),
            await ConfirmAsync(client, seeded.OrderIds[1], otherRestaurant.DeliveryAddressId),
            await ConfirmAsync(client, seeded.OrderIds[2], Guid.NewGuid())
        };

        responses.Should().OnlyContain(
            response => response.StatusCode == HttpStatusCode.NotFound);
        foreach (var response in responses)
            (await response.Content.ReadFromJsonAsync<ErrorEnvelope>())!
                .Error!.Code.Should().Be("DELIVERY_ADDRESS_NOT_FOUND");
    }

    private HttpClient Client(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static Task<HttpResponseMessage> ConfirmAsync(
        HttpClient client, Guid orderId, Guid deliveryAddressId) =>
        client.PostAsJsonAsync(
            $"/api/v1/orders/{orderId}/confirm", new { deliveryAddressId });

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

        var token = await LoginAsync(client, email, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var addressResponse = await client.PostAsJsonAsync(
            "/api/v1/restaurants/me/delivery-addresses",
            new
            {
                addressLine = "1 Original Street",
                recipientName = "Original Recipient",
                phone = "0901234567",
                latitude = 10.123456m,
                longitude = 106.123456m,
                isDefault = false
            });
        addressResponse.EnsureSuccessStatusCode();
        var address = await addressResponse.Content
            .ReadFromJsonAsync<Envelope<DeliveryAddressBody>>();

        return new RestaurantIdentity(restaurantId, address!.Data!.Id, token);
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

    private async Task<SeededBatchedOrders> SeedBatchedOrdersAsync(
        Guid restaurantId,
        int stock,
        IReadOnlyList<int> quantities,
        int? reservedQuantity = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var marketProduct = await AddMarketProductAsync(db, stock);
        var orders = quantities.Select(quantity =>
        {
            var order = new Order(restaurantId, null, null);
            order.AddItem(marketProduct.Id, "Cà chua", quantity, marketProduct.CurrentPrice);
            order.Confirm();
            order.AdvanceStatus(OrderStatus.Batched);
            order.ClearDomainEvents();
            return order;
        }).ToArray();

        db.Set<Order>().AddRange(orders);
        await db.SaveChangesAsync();
        var reserved = reservedQuantity ?? quantities.Sum();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE market_products SET \"ReservedQuantity\" = {reserved} WHERE \"Id\" = {marketProduct.Id}");
        return new SeededBatchedOrders(
            marketProduct.Id,
            orders.Select(order => order.Id).ToArray());
    }

    private static async Task<MarketProduct> AddMarketProductAsync(AppDbContext db, int stock)
    {
        var unit = new UnitOfMeasurement($"kg-{Guid.NewGuid():N}", "kg");
        var market = new Market(
            $"Stock Market {Guid.NewGuid():N}", "HCMC", "1 Test Street",
            10.123456m, 106.123456m);
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

    private sealed record RestaurantIdentity(
        Guid RestaurantId, Guid DeliveryAddressId, string Token);
    private sealed record SeededOrders(IReadOnlyList<Guid> MarketProductIds, IReadOnlyList<Guid> OrderIds);
    private sealed record SeededMultiLineOrder(IReadOnlyList<Guid> MarketProductIds, Guid OrderId);
    private sealed record SeededBatchedOrder(Guid MarketProductId, Guid OrderId);
    private sealed record SeededBatchedOrders(
        Guid MarketProductId,
        IReadOnlyList<Guid> OrderIds);
    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);
    private sealed record UserSummaryBody(Guid? RestaurantId);
    private sealed record DeliveryAddressBody(Guid Id);
}
