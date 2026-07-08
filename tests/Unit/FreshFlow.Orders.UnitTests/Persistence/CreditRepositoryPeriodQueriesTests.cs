using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Orders.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Orders.UnitTests.Persistence;

/// <summary>
/// Unit tests for the <see cref="CreditRepository"/> methods added for statement
/// generation (SCRUM-261): period-bounded transaction reads and the net-balance-movement
/// fallback used as the opening balance for a restaurant's very first statement.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CreditRepositoryPeriodQueriesTests
{
    private static readonly Guid RestaurantId = Guid.NewGuid();

    private static (CreditRepository Repo, AppDbContext Ctx) BuildSut(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var ctx = new AppDbContext(options);
        return (new CreditRepository(ctx), ctx);
    }

    [Fact]
    public async Task GetTransactionsInPeriodAsync_ExcludesRowsOutsidePeriodAsync()
    {
        var (sut, ctx) = BuildSut($"db-{Guid.NewGuid()}");
        var now = DateTime.UtcNow;
        var inPeriod = new CreditTransaction(RestaurantId, Guid.NewGuid(), CreditTransactionType.Charge, 10m, 10m, null);
        ctx.Set<CreditTransaction>().Add(inPeriod);
        await ctx.SaveChangesAsync();

        // The whole period brackets "now" so the row we just created falls inside it.
        var items = await sut.GetTransactionsInPeriodAsync(
            RestaurantId, now.AddMinutes(-5), now.AddMinutes(5), CancellationToken.None);

        items.Should().ContainSingle();

        // A period entirely in the past excludes the same row.
        var empty = await sut.GetTransactionsInPeriodAsync(
            RestaurantId, now.AddDays(-2), now.AddDays(-1), CancellationToken.None);

        empty.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTransactionsInPeriodAsync_ExcludesAdjustmentRowsAsync()
    {
        var (sut, ctx) = BuildSut($"db-{Guid.NewGuid()}");
        var now = DateTime.UtcNow;
        ctx.Set<CreditTransaction>().AddRange(
            new CreditTransaction(RestaurantId, Guid.NewGuid(), CreditTransactionType.Charge, 10m, 10m, null),
            new CreditTransaction(RestaurantId, null, CreditTransactionType.Adjustment, 500m, 10m, null));
        await ctx.SaveChangesAsync();

        var items = await sut.GetTransactionsInPeriodAsync(
            RestaurantId, now.AddMinutes(-5), now.AddMinutes(5), CancellationToken.None);

        items.Should().ContainSingle();
        items.Single().Type.Should().Be(CreditTransactionType.Charge);
    }

    [Fact]
    public async Task GetTransactionsInPeriodAsync_OrdersChronologicallyAscendingAsync()
    {
        var (sut, ctx) = BuildSut($"db-{Guid.NewGuid()}");
        var now = DateTime.UtcNow;
        var first = new CreditTransaction(RestaurantId, Guid.NewGuid(), CreditTransactionType.Charge, 10m, 10m, "first");
        var second = new CreditTransaction(RestaurantId, Guid.NewGuid(), CreditTransactionType.Charge, 20m, 30m, "second");
        ctx.Set<CreditTransaction>().AddRange(first, second);
        await ctx.SaveChangesAsync();

        var items = await sut.GetTransactionsInPeriodAsync(
            RestaurantId, now.AddMinutes(-5), now.AddMinutes(5), CancellationToken.None);

        items.Should().HaveCount(2);
        items[0].CreatedAt.Should().BeOnOrBefore(items[1].CreatedAt);
    }

    [Fact]
    public async Task GetNetBalanceMovementBeforeAsync_SumsChargeMinusSettlementAndRefundAsync()
    {
        var (sut, ctx) = BuildSut($"db-{Guid.NewGuid()}");
        var future = DateTime.UtcNow.AddDays(1); // strictly after all rows below
        ctx.Set<CreditTransaction>().AddRange(
            new CreditTransaction(RestaurantId, Guid.NewGuid(), CreditTransactionType.Charge, 100m, 100m, null),
            new CreditTransaction(RestaurantId, Guid.NewGuid(), CreditTransactionType.Charge, 50m, 150m, null),
            new CreditTransaction(
                RestaurantId, null, CreditTransactionType.Settlement, 40m, 110m, null, PaymentMethod.Manual),
            new CreditTransaction(RestaurantId, Guid.NewGuid(), CreditTransactionType.Refund, 10m, 100m, null));
        await ctx.SaveChangesAsync();

        var net = await sut.GetNetBalanceMovementBeforeAsync(RestaurantId, future, CancellationToken.None);

        net.Should().Be(100m); // 100 + 50 - 40 - 10
    }

    [Fact]
    public async Task GetNetBalanceMovementBeforeAsync_NoRows_ReturnsZeroAsync()
    {
        var (sut, _) = BuildSut($"db-{Guid.NewGuid()}");

        var net = await sut.GetNetBalanceMovementBeforeAsync(RestaurantId, DateTime.UtcNow, CancellationToken.None);

        net.Should().Be(0m);
    }

    [Fact]
    public async Task GetActiveRestaurantIdsAsync_ReturnsRestaurantIdsWithCreditAccountsAsync()
    {
        var (sut, ctx) = BuildSut($"db-{Guid.NewGuid()}");
        var otherRestaurantId = Guid.NewGuid();
        ctx.Set<RestaurantCredit>().AddRange(
            new RestaurantCredit(RestaurantId, creditLimit: 100m),
            new RestaurantCredit(otherRestaurantId, creditLimit: 200m));
        await ctx.SaveChangesAsync();

        var ids = await sut.GetActiveRestaurantIdsAsync(CancellationToken.None);

        ids.Should().BeEquivalentTo([RestaurantId, otherRestaurantId]);
    }
}
