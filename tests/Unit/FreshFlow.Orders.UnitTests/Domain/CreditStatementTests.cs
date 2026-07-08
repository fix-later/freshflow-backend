using FluentAssertions;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class CreditStatementTests
{
    private static readonly DateTime PeriodStart = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    private static CreditStatementLine SampleLine(CreditTransactionType type, decimal amount, decimal balanceAfter) =>
        new(Guid.NewGuid(), type, amount, balanceAfter, PeriodStart.AddDays(1), null, null);

    [Fact]
    public void Constructor_WithEmptyRestaurantId_Throws()
    {
        var act = () => new CreditStatement(
            Guid.Empty, PeriodStart, PeriodEnd, 0m, 0m, 0m, 0m, []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithPeriodStartNotBeforePeriodEnd_Throws()
    {
        var act = () => new CreditStatement(
            Guid.NewGuid(), PeriodEnd, PeriodStart, 0m, 0m, 0m, 0m, []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNegativeOpeningBalance_Throws()
    {
        var act = () => new CreditStatement(
            Guid.NewGuid(), PeriodStart, PeriodEnd, -1m, 0m, 0m, 0m, []);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeTotal_Throws()
    {
        var act = () => new CreditStatement(
            Guid.NewGuid(), PeriodStart, PeriodEnd, 0m, -1m, 0m, 0m, []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ClosingBalanceWouldBeNegative_Throws()
    {
        var act = () => new CreditStatement(
            Guid.NewGuid(), PeriodStart, PeriodEnd, 0m, 0m, 100m, 0m, []);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ComputesClosingBalanceFromOpeningAndTotals()
    {
        var statement = new CreditStatement(
            Guid.NewGuid(), PeriodStart, PeriodEnd,
            openingBalance: 100m, totalCharges: 200m, totalSettlements: 150m, totalRefunds: 20m, []);

        statement.ClosingBalance.Should().Be(130m); // 100 + 200 - 150 - 20
    }

    [Fact]
    public void Constructor_StoresLinesAndStampsGeneratedAt()
    {
        var lines = new List<CreditStatementLine>
        {
            SampleLine(CreditTransactionType.Charge, 100m, 100m),
            SampleLine(CreditTransactionType.Settlement, 40m, 60m),
        };
        var before = DateTime.UtcNow;

        var statement = new CreditStatement(
            Guid.NewGuid(), PeriodStart, PeriodEnd, 0m, 100m, 40m, 0m, lines);

        statement.Lines.Should().HaveCount(2);
        statement.GeneratedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }
}
