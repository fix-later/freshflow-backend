using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Orders.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Orders.UnitTests.Persistence;

/// <summary>
/// Verifies that <see cref="CreditRepository.GetTransactionsPageAsync"/> defensively
/// filters out non-balance-moving rows (Adjustment) so legacy credit-limit-change rows
/// never pollute the balance ledger, even though CreditService no longer writes them.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CreditRepositoryLedgerFilterTests
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
    public async Task GetTransactionsPageAsync_LegacyAdjustmentRowPresent_ExcludesItFromLedgerAsync()
    {
        // Arrange — a legacy Adjustment row alongside balance-moving rows.
        var (sut, ctx) = BuildSut($"db-{Guid.NewGuid()}");
        var charge = new CreditTransaction(
            RestaurantId, Guid.NewGuid(), CreditTransactionType.Charge, 100m, 100m, "Charge");
        var adjustment = new CreditTransaction(
            RestaurantId, null, CreditTransactionType.Adjustment, 500m, 100m, "Limit changed");
        var settlement = new CreditTransaction(
            RestaurantId, null, CreditTransactionType.Settlement, 40m, 60m, "Settled");
        ctx.Set<CreditTransaction>().AddRange(charge, adjustment, settlement);
        await ctx.SaveChangesAsync();

        // Act
        var (items, nextCursor) = await sut.GetTransactionsPageAsync(
            RestaurantId, null, 50, null, null, CancellationToken.None);

        // Assert
        items.Should().HaveCount(2);
        items.Select(t => t.Type).Should().NotContain(CreditTransactionType.Adjustment);
        nextCursor.Should().BeNull();
    }
}
