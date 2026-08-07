using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Pricing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Orders;

/// <summary>
/// SCRUM-386 — proves the auto-confirm pipeline end to end against real Postgres: a due
/// occurrence with an item template + delivery address becomes a Confirmed order with credit
/// charged and stock reserved, while a template that can't clear credit degrades to a Draft and
/// still records the schedule's execution (idempotency preserved).
/// </summary>
[Trait("Category", "Integration")]
public sealed class ScheduledOrderAutoConfirmTests(AuthWebAppFactory factory) : IClassFixture<AuthWebAppFactory>
{
    [Fact]
    public async Task GenerateDueAsync_ScheduleWithItemsAndAddress_ConfirmsOrderChargesCreditAndReservesStockAsync()
    {
        var restaurant = await CreateRestaurantAsync();
        var firstRun = DateTime.UtcNow.AddMinutes(-1);
        Guid scheduledOrderId;
        Guid marketProductId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var marketProduct = await AddMarketProductAsync(db, stock: 10);
            marketProductId = marketProduct.Id;

            var schedule = new ScheduledOrder(
                restaurant.RestaurantId, RecurrenceType.Daily, firstRun, "auto", restaurant.DeliveryAddressId);
            schedule.AddItem(marketProductId, 2);
            db.Set<ScheduledOrder>().Add(schedule);
            db.Set<RestaurantCredit>().Add(new RestaurantCredit(restaurant.RestaurantId, 1_000_000m));
            await db.SaveChangesAsync();
            scheduledOrderId = schedule.Id;
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var generator = scope.ServiceProvider.GetRequiredService<IScheduledOrderGenerationService>();
            var result = await generator.GenerateDueAsync(DateTime.UtcNow, default);
            result.CreatedOrderCount.Should().Be(1);
        }

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await verifyDb.Set<Order>().AsNoTracking()
            .Include(o => o.Items)
            .SingleAsync(o => o.ScheduledOrderId == scheduledOrderId);
        var product = await verifyDb.Set<MarketProduct>().AsNoTracking()
            .SingleAsync(p => p.Id == marketProductId);
        var account = await verifyDb.Set<RestaurantCredit>().AsNoTracking()
            .SingleAsync(a => a.RestaurantId == restaurant.RestaurantId);
        var schedule2 = await verifyDb.Set<ScheduledOrder>().AsNoTracking()
            .SingleAsync(s => s.Id == scheduledOrderId);

        order.Status.Should().Be(OrderStatus.Confirmed);
        order.Items.Should().ContainSingle(i => i.MarketProductId == marketProductId && i.Quantity == 2);
        product.ReservedQuantity.Should().Be(2);
        account.OutstandingBalance.Should().BeGreaterThan(0m);
        schedule2.LastExecutedAt.Should().BeCloseTo(firstRun, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task GenerateDueAsync_CreditLimitExceeded_DegradesToDraftAndStillRecordsExecutionAsync()
    {
        var restaurant = await CreateRestaurantAsync();
        var firstRun = DateTime.UtcNow.AddMinutes(-1);
        Guid scheduledOrderId;
        Guid marketProductId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var marketProduct = await AddMarketProductAsync(db, stock: 10);
            marketProductId = marketProduct.Id;

            var schedule = new ScheduledOrder(
                restaurant.RestaurantId, RecurrenceType.Daily, firstRun, "auto", restaurant.DeliveryAddressId);
            schedule.AddItem(marketProductId, 2);
            db.Set<ScheduledOrder>().Add(schedule);
            // Credit limit of 0 guarantees CanChargeAsync fails.
            db.Set<RestaurantCredit>().Add(new RestaurantCredit(restaurant.RestaurantId, 0m));
            await db.SaveChangesAsync();
            scheduledOrderId = schedule.Id;
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var generator = scope.ServiceProvider.GetRequiredService<IScheduledOrderGenerationService>();
            var result = await generator.GenerateDueAsync(DateTime.UtcNow, default);
            result.CreatedOrderCount.Should().Be(1);
        }

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await verifyDb.Set<Order>().AsNoTracking()
            .SingleAsync(o => o.ScheduledOrderId == scheduledOrderId);
        var product = await verifyDb.Set<MarketProduct>().AsNoTracking()
            .SingleAsync(p => p.Id == marketProductId);
        var schedule2 = await verifyDb.Set<ScheduledOrder>().AsNoTracking()
            .SingleAsync(s => s.Id == scheduledOrderId);

        order.Status.Should().Be(OrderStatus.Draft);
        product.ReservedQuantity.Should().Be(0);
        // Idempotency preserved even on the degrade path — a re-run must not create a duplicate.
        schedule2.LastExecutedAt.Should().BeCloseTo(firstRun, TimeSpan.FromMilliseconds(1));
    }

    private async Task<RestaurantIdentity> CreateRestaurantAsync()
    {
        using var client = factory.CreateClient();
        var adminToken = await LoginAsync(client, "admin@test.freshflow", "AdminP@ss1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"scheduled-auto-{Guid.NewGuid():N}@test.freshflow";
        const string password = "RestaurantP@ss1";
        (await client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password,
            role = "restaurant",
            restaurantName = $"Scheduled Auto Test {Guid.NewGuid():N}"
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
                addressLine = "1 Scheduled Street",
                recipientName = "Scheduled Recipient",
                phone = "0901234567",
                latitude = 10.123456m,
                longitude = 106.123456m,
                isDefault = false
            });
        addressResponse.EnsureSuccessStatusCode();
        var address = await addressResponse.Content.ReadFromJsonAsync<Envelope<DeliveryAddressBody>>();

        return new RestaurantIdentity(restaurantId, address!.Data!.Id);
    }

    private static async Task<string> LoginAsync(HttpClient client, string identifier, string password)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { identifier, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Envelope<TokenBody>>())!.Data!.AccessToken;
    }

    private static async Task<MarketProduct> AddMarketProductAsync(AppDbContext db, int stock)
    {
        var unit = new UnitOfMeasurement($"kg-{Guid.NewGuid():N}", "kg");
        var market = new Market(
            $"Scheduled Auto Market {Guid.NewGuid():N}", "HCMC", "1 Test Street",
            10.123456m, 106.123456m);
        db.Set<UnitOfMeasurement>().Add(unit);
        db.Set<Market>().Add(market);
        await db.SaveChangesAsync();

        var product = new Product($"Scheduled Auto Product {Guid.NewGuid():N}", unit.Id, null, null, null);
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync();

        var marketProduct = new MarketProduct(market.Id, product.Id, 10_000m, stock, null);
        db.Set<MarketProduct>().Add(marketProduct);
        await db.SaveChangesAsync();
        return marketProduct;
    }

    private sealed record RestaurantIdentity(Guid RestaurantId, Guid DeliveryAddressId);
    private sealed record UserListBody(IReadOnlyList<UserSummaryBody> Data);
    private sealed record UserSummaryBody(Guid? RestaurantId);
    private sealed record DeliveryAddressBody(Guid Id);
}
