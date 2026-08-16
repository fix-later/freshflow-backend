using FluentAssertions;
using FreshFlow.Orders.Application.Services;

namespace FreshFlow.Orders.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class CreditStatementPeriodCalculatorTests
{
    [Fact]
    public void ResolvePeriod_JuneVN_ReturnsUtcMonthBoundariesShiftedBySevenHours()
    {
        // Asia/Ho_Chi_Minh is UTC+7 year-round (no DST) — 2026-06-01T00:00 local
        // is 2026-05-31T17:00 UTC.
        var (periodStart, periodEnd) = CreditStatementPeriodCalculator.ResolvePeriod(2026, 6);

        periodStart.Should().Be(new DateTime(2026, 5, 31, 17, 0, 0, DateTimeKind.Utc));
        periodEnd.Should().Be(new DateTime(2026, 6, 30, 17, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ResolveDueDate_IsPeriodEndPlusPaymentTermDays()
    {
        var (_, periodEnd) = CreditStatementPeriodCalculator.ResolvePeriod(2026, 6);

        var dueDate = CreditStatementPeriodCalculator.ResolveDueDate(periodEnd);

        dueDate.Should().Be(periodEnd.AddDays(CreditStatementPeriodCalculator.PaymentTermDays));
        dueDate.Should().Be(new DateTime(2026, 7, 15, 17, 0, 0, DateTimeKind.Utc)); // 2026-06-30T17:00Z + 15d
    }

    [Fact]
    public void ResolvePeriod_December_RollsOverToNextYearForPeriodEnd()
    {
        var (periodStart, periodEnd) = CreditStatementPeriodCalculator.ResolvePeriod(2026, 12);

        periodStart.Should().Be(new DateTime(2026, 11, 30, 17, 0, 0, DateTimeKind.Utc));
        periodEnd.Should().Be(new DateTime(2026, 12, 31, 17, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ResolvePeriod_PeriodStartIsAlwaysBeforePeriodEnd()
    {
        var (periodStart, periodEnd) = CreditStatementPeriodCalculator.ResolvePeriod(2026, 1);

        periodStart.Should().BeBefore(periodEnd);
    }
}
