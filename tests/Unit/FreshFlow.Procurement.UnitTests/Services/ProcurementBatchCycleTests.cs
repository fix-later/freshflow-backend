using FluentAssertions;
using FreshFlow.Procurement.Application.Services;

namespace FreshFlow.Procurement.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class ProcurementBatchCycleTests
{
    [Fact]
    public void ResolveDueBatchDate_At2159Hcmc_ReturnsCurrentDeliveryDate()
    {
        var utc = new DateTimeOffset(2026, 7, 14, 14, 59, 0, TimeSpan.Zero);

        var result = ProcurementBatchCycle.ResolveDueBatchDate(
            utc,
            new TimeOnly(22, 0));

        result.Should().Be(new DateOnly(2026, 7, 14));
    }

    [Fact]
    public void ResolveDueBatchDate_At2201Hcmc_ReturnsNextDeliveryDate()
    {
        var utc = new DateTimeOffset(2026, 7, 14, 15, 1, 0, TimeSpan.Zero);

        var result = ProcurementBatchCycle.ResolveDueBatchDate(
            utc,
            new TimeOnly(22, 0));

        result.Should().Be(new DateOnly(2026, 7, 15));
    }
}
