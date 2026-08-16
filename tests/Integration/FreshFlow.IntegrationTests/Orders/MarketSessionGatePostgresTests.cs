using FluentAssertions;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Catalog.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.IntegrationTests.Infrastructure;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Pricing.Domain.Entities;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.IntegrationTests.Orders;

[Trait("Category", "Integration")]
public sealed class MarketSessionGatePostgresTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    [Fact]
    public async Task ShareLock_BlocksCloseUntilConfirmationTransactionCommitsAsync()
    {
        var marketId = Guid.NewGuid();
        var date = new DateOnly(2026, 8, 20);
        using var confirmationScope = factory.Services.CreateScope();
        var confirmationDb = confirmationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = MarketSession.Create(
            marketId,
            Guid.NewGuid(),
            date,
            new DateTime(2026, 8, 19, 15, 0, 0, DateTimeKind.Utc),
            MarketSessionCreatedSource.Manual,
            true).Value;
        confirmationDb.Add(session);
        await confirmationDb.SaveChangesAsync();
        confirmationScope.ServiceProvider.GetRequiredService<IConfiguration>()
            ["Orders:MarketSessions:Enforce"] = "true";

        await using var confirmationTransaction = await confirmationDb.Database.BeginTransactionAsync();
        var gate = confirmationScope.ServiceProvider.GetRequiredService<IMarketSessionGate>();
        (await gate.CheckAsync(marketId, date, true, default)).IsOpen.Should().BeTrue();

        using var closeScope = factory.Services.CreateScope();
        var closeDb = closeScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var closeTask = closeDb.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE market_sessions SET status = 'Closed' WHERE id = {session.Id}");
        (await Task.WhenAny(closeTask, Task.Delay(200))).Should().NotBe(closeTask);

        await confirmationTransaction.CommitAsync();
        (await closeTask).Should().Be(1);
        (await gate.CheckAsync(marketId, date, false, default)).IsOpen.Should().BeFalse();

        confirmationScope.ServiceProvider.GetRequiredService<IConfiguration>()
            ["Orders:MarketSessions:Enforce"] = "false";
        var bypassed = await gate.CheckAsync(marketId, date, false, default);
        bypassed.IsOpen.Should().BeTrue();
        bypassed.SessionId.Should().Be(session.Id);
    }

    [Fact]
    public async Task CheckAsync_ReportsPlannedCapacityAndSumsConfirmedGoodsKgAsync()
    {
        var marketId = Guid.NewGuid();
        var date = new DateOnly(2026, 8, 21);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        scope.ServiceProvider.GetRequiredService<IConfiguration>()
            ["Orders:MarketSessions:Enforce"] = "true";

        var session = MarketSession.Create(
            marketId,
            Guid.NewGuid(),
            date,
            new DateTime(2026, 8, 20, 15, 0, 0, DateTimeKind.Utc),
            MarketSessionCreatedSource.Manual,
            true).Value;
        db.Add(session);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE market_sessions SET planned_capacity_kg = {500m} WHERE id = {session.Id}");

        var owner = await db.Set<User>().AsNoTracking()
            .SingleAsync(user => user.Email == "admin@test.freshflow");
        var restaurantId = Guid.NewGuid();
        var seedNow = DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO restaurants ("Id", "Name", "UserId", "CreatedAt", "UpdatedAt")
            VALUES ({restaurantId}, {"Capacity Test"}, {owner.Id}, {seedNow}, {seedNow})
            """);

        var unit = new UnitOfMeasurement($"kg-{Guid.NewGuid():N}", "kg");
        var market = new Market($"Cap Market {Guid.NewGuid():N}", "HCMC", "1 Test St", null, null);
        db.Set<UnitOfMeasurement>().Add(unit);
        db.Set<Market>().Add(market);
        await db.SaveChangesAsync();
        var product = new Product("Seed goods", unit.Id, null, null, null);
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync();
        var marketProduct = new MarketProduct(market.Id, product.Id, 10_000m, 10_000, null);
        db.Set<MarketProduct>().Add(marketProduct);
        await db.SaveChangesAsync();

        // A confirmed order (120 kg) counts; a cancelled order (999 kg) must be excluded by the SUM.
        await SeedOrderWithItemAsync(db, session.Id, restaurantId, marketProduct.Id, "Confirmed", 120);
        await SeedOrderWithItemAsync(db, session.Id, restaurantId, marketProduct.Id, "Cancelled", 999);

        var gate = scope.ServiceProvider.GetRequiredService<IMarketSessionGate>();
        var result = await gate.CheckAsync(marketId, date, true, default);

        result.Exists.Should().BeTrue();
        result.PlannedCapacityKg.Should().Be(500m);
        result.ConfirmedGoodsKg.Should().Be(120m);
    }

    private static async Task SeedOrderWithItemAsync(
        AppDbContext db, Guid marketSessionId, Guid restaurantId, Guid marketProductId,
        string status, int quantityKg)
    {
        var order = new Order(restaurantId, DateTime.UtcNow, null);
        order.AddItem(marketProductId, "Seed goods", quantityKg, 10_000m).IsSuccess.Should().BeTrue();
        order.ClearDomainEvents();
        db.Add(order);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE orders SET "Status" = {status}, market_session_id = {marketSessionId} WHERE "Id" = {order.Id}""");
    }
}
