using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Orders.UnitTests.Persistence;

[Trait("Category", "Unit")]
public sealed class CreditStatementRepositoryTests
{
    private static readonly Guid RestaurantId = Guid.NewGuid();

    private static (CreditStatementRepository Repo, AppDbContext Ctx) BuildSut(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var ctx = new AppDbContext(options);
        return (new CreditStatementRepository(ctx), ctx);
    }

    private static CreditStatement NewStatement(Guid restaurantId, int year, int month, decimal closingBalance) =>
        new(
            restaurantId,
            new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1),
            openingBalance: 0m,
            totalCharges: closingBalance,
            totalSettlements: 0m,
            totalRefunds: 0m,
            []);

    [Fact]
    public async Task FindByPeriodAsync_ExistingPeriod_ReturnsStatementWithLinesAsync()
    {
        var (sut, ctx) = BuildSut($"db-{Guid.NewGuid()}");
        var line = new CreditStatementLine(
            Guid.NewGuid(), FreshFlow.Orders.Domain.Enums.CreditTransactionType.Charge, 50m, 50m,
            DateTime.UtcNow, null, null);
        var statement = new CreditStatement(
            RestaurantId,
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            0m, 50m, 0m, 0m, [line]);
        ctx.Set<CreditStatement>().Add(statement);
        await ctx.SaveChangesAsync();

        var found = await sut.FindByPeriodAsync(
            RestaurantId, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        found.Should().NotBeNull();
        found!.Id.Should().Be(statement.Id);
        found.Lines.Should().ContainSingle();
    }

    [Fact]
    public async Task FindByPeriodAsync_NoMatchingPeriod_ReturnsNullAsync()
    {
        var (sut, _) = BuildSut($"db-{Guid.NewGuid()}");

        var found = await sut.FindByPeriodAsync(RestaurantId, DateTime.UtcNow, CancellationToken.None);

        found.Should().BeNull();
    }

    [Fact]
    public async Task GetPageAsync_ZeroPageSize_ThrowsArgumentExceptionAsync()
    {
        var (sut, _) = BuildSut($"db-{Guid.NewGuid()}");

        var act = async () => await sut.GetPageAsync(RestaurantId, null, 0, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("pageSize");
    }

    [Fact]
    public async Task GetPageAsync_OrdersByPeriodStartDescendingAsync()
    {
        var (sut, ctx) = BuildSut($"db-{Guid.NewGuid()}");
        ctx.Set<CreditStatement>().AddRange(
            NewStatement(RestaurantId, 2026, 1, 10m),
            NewStatement(RestaurantId, 2026, 3, 30m),
            NewStatement(RestaurantId, 2026, 2, 20m));
        await ctx.SaveChangesAsync();

        var (items, nextCursor) = await sut.GetPageAsync(RestaurantId, null, 50, CancellationToken.None);

        items.Should().HaveCount(3);
        items.Select(s => s.PeriodStart.Month).Should().ContainInOrder(3, 2, 1);
        nextCursor.Should().BeNull();
    }

    [Fact]
    public async Task GetPageAsync_MoreRowsThanPageSize_ReturnsNextCursorAsync()
    {
        var (sut, ctx) = BuildSut($"db-{Guid.NewGuid()}");
        ctx.Set<CreditStatement>().AddRange(
            NewStatement(RestaurantId, 2026, 1, 10m),
            NewStatement(RestaurantId, 2026, 2, 20m),
            NewStatement(RestaurantId, 2026, 3, 30m));
        await ctx.SaveChangesAsync();

        var (items, nextCursor) = await sut.GetPageAsync(RestaurantId, null, 2, CancellationToken.None);

        items.Should().HaveCount(2);
        nextCursor.Should().NotBeNull();

        var (nextItems, secondCursor) = await sut.GetPageAsync(RestaurantId, nextCursor, 2, CancellationToken.None);
        nextItems.Should().ContainSingle();
        secondCursor.Should().BeNull();
    }

    [Theory]
    [InlineData("not-valid-base64!@#$")]
    [InlineData("")]
    public async Task GetPageAsync_InvalidOrTamperedCursor_TreatedAsStartOfListAsync(string cursor)
    {
        var (sut, _) = BuildSut($"db-{Guid.NewGuid()}");

        var act = async () => await sut.GetPageAsync(RestaurantId, cursor, 10, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
