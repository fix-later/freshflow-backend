using FluentAssertions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Services;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Orders.Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Persistence;

/// <summary>
/// End-to-end regression test (real EF repositories, not mocks) for the DEC-CRE-06
/// opening-balance bug found in review: generating a statement must always source its
/// opening balance from the ledger, never from a prior statement's ClosingBalance, because
/// a skipped period would otherwise silently and PERMANENTLY drop that period's ledger
/// movements from every later (immutable) statement.
///
/// Scenario: Jan statement generated normally; Feb is SKIPPED (no statement, but real
/// ledger activity happens); Mar is then generated. Mar's OpeningBalance must reflect
/// Feb's ledger movement, not Jan's stale ClosingBalance.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CreditStatementGenerationGapScenarioTests
{
    private static readonly Guid RestaurantId = Guid.NewGuid();

    private static (CreditStatementGenerationService Sut, AppDbContext Ctx) BuildSut(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var ctx = new AppDbContext(options);
        var creditRepository = new CreditRepository(ctx);
        var statementRepository = new CreditStatementRepository(ctx);
        var sut = new CreditStatementGenerationService(
            statementRepository, creditRepository, Substitute.For<IPublisher>());
        return (sut, ctx);
    }

    private static CreditTransaction NewTransaction(
        Guid restaurantId, CreditTransactionType type, decimal amount, decimal balanceAfter, DateTime createdAt)
    {
        var transaction = new CreditTransaction(restaurantId, null, type, amount, balanceAfter, null);
        SetCreatedAt(transaction, createdAt);
        return transaction;
    }

    private static void SetCreatedAt(CreditTransaction transaction, DateTime createdAt) =>
        typeof(CreditTransaction)
            .GetField($"<{nameof(CreditTransaction.CreatedAt)}>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(transaction, createdAt);

    [Fact]
    public async Task GenerateAsync_PeriodSkippedBetweenTwoGeneratedStatements_OpeningBalanceReflectsSkippedPeriodLedgerAsync()
    {
        var (sut, ctx) = BuildSut($"db-{Guid.NewGuid()}");

        // Jan 2020: a single 500 charge — Jan's statement will close at 500.
        var januaryCharge = NewTransaction(
            RestaurantId, CreditTransactionType.Charge, 500m, 500m,
            new DateTime(2020, 1, 15, 0, 0, 0, DateTimeKind.Utc));

        // Feb 2020: a 200 settlement — SKIPPED, no statement ever generated for this period.
        var februarySettlement = NewTransaction(
            RestaurantId, CreditTransactionType.Settlement, 200m, 300m,
            new DateTime(2020, 2, 15, 0, 0, 0, DateTimeKind.Utc));

        ctx.Set<CreditTransaction>().AddRange(januaryCharge, februarySettlement);
        await ctx.SaveChangesAsync();

        var januaryResult = await sut.GenerateAsync(RestaurantId, 2020, 1, CancellationToken.None);
        januaryResult.IsSuccess.Should().BeTrue();
        januaryResult.Value.ClosingBalance.Should().Be(500m);

        // February is intentionally never generated — simulates the monthly job missing a
        // run, or an admin/owner calling on-demand generate out of order.

        var marchResult = await sut.GenerateAsync(RestaurantId, 2020, 3, CancellationToken.None);

        marchResult.IsSuccess.Should().BeTrue();
        marchResult.Value.OpeningBalance.Should().Be(300m, // 500 (Jan charge) - 200 (Feb settlement)
            "opening balance must come from the ledger and include Feb's settlement, " +
            "not silently carry Jan's stale 500 ClosingBalance forward");
        marchResult.Value.OpeningBalance.Should().NotBe(januaryResult.Value.ClosingBalance);
    }
}
