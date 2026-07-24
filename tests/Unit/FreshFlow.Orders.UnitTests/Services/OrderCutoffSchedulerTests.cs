using FluentAssertions;
using FreshFlow.Orders.Application.Services;

namespace FreshFlow.Orders.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class OrderCutoffSchedulerTests
{
    // Asia/Ho_Chi_Minh is UTC+7 — 14:59 UTC == 21:59 local (2026-06-18), 15:00 UTC == 22:00 local (cutoff).
    // D = 2026-06-18 local. D+1 local midnight == 17:00 UTC on 2026-06-18. D+2 == 17:00 UTC on 2026-06-19.
    private static readonly DateTime BeforeCutoffUtc = new(2026, 6, 18, 14, 59, 0, DateTimeKind.Utc);
    private static readonly DateTime AtCutoffUtc = new(2026, 6, 18, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime AfterCutoffUtc = new(2026, 6, 18, 16, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DPlus1Utc = new(2026, 6, 18, 17, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DPlus2Utc = new(2026, 6, 19, 17, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DPlus7Utc = new(2026, 6, 24, 17, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ResolveScheduledFor_BeforeCutoff_NoRequestedSchedule_ReturnsDPlus1()
    {
        var result = OrderCutoffScheduler.ResolveScheduledFor(BeforeCutoffUtc, requestedScheduledFor: null);

        result.Should().Be(DPlus1Utc);
    }

    [Fact]
    public void ResolveScheduledFor_AtCutoff_NoRequestedSchedule_ReturnsDPlus2()
    {
        var result = OrderCutoffScheduler.ResolveScheduledFor(AtCutoffUtc, requestedScheduledFor: null);

        result.Should().Be(DPlus2Utc);
    }

    [Fact]
    public void ResolveScheduledFor_AfterCutoff_NoRequestedSchedule_ReturnsDPlus2()
    {
        var result = OrderCutoffScheduler.ResolveScheduledFor(AfterCutoffUtc, requestedScheduledFor: null);

        result.Should().Be(DPlus2Utc);
    }

    [Fact]
    public void ResolveScheduledFor_BeforeCutoff_RequestedWithinWindow_KeepsRequestedSchedule()
    {
        var requested = DPlus2Utc.AddHours(10);

        var result = OrderCutoffScheduler.ResolveScheduledFor(BeforeCutoffUtc, requested);

        result.Should().Be(requested);
    }

    [Fact]
    public void ResolveScheduledFor_BeforeCutoff_RequestedEarlierThanDPlus1_NormalizesUpToDPlus1()
    {
        var requested = BeforeCutoffUtc.AddHours(1);

        var result = OrderCutoffScheduler.ResolveScheduledFor(BeforeCutoffUtc, requested);

        result.Should().Be(DPlus1Utc);
    }

    [Fact]
    public void ResolveScheduledFor_AfterCutoff_RequestedEarlierThanDPlus2_NormalizesUpToDPlus2()
    {
        var requested = DPlus1Utc.AddHours(2);

        var result = OrderCutoffScheduler.ResolveScheduledFor(AfterCutoffUtc, requested);

        result.Should().Be(DPlus2Utc);
    }

    [Fact]
    public void ResolveScheduledFor_RequestedAtUpperBoundDPlus7_KeepsRequestedSchedule()
    {
        var result = OrderCutoffScheduler.ResolveScheduledFor(BeforeCutoffUtc, DPlus7Utc);

        result.Should().Be(DPlus7Utc);
    }

    [Fact]
    public void IsWithinDeliveryWindow_AtUpperBoundDPlus7_ReturnsTrue()
    {
        var result = OrderCutoffScheduler.IsWithinDeliveryWindow(BeforeCutoffUtc, DPlus7Utc, windowDays: 7);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsWithinDeliveryWindow_BeyondDPlus7_ReturnsFalse()
    {
        var beyondWindow = DPlus7Utc.AddSeconds(1);

        var result = OrderCutoffScheduler.IsWithinDeliveryWindow(BeforeCutoffUtc, beyondWindow, windowDays: 7);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsWithinDeliveryWindow_InThePast_ReturnsFalse()
    {
        var pastDate = BeforeCutoffUtc.AddHours(-1);

        var result = OrderCutoffScheduler.IsWithinDeliveryWindow(BeforeCutoffUtc, pastDate, windowDays: 7);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsWithinDeliveryWindow_EqualToNow_ReturnsTrue()
    {
        var result = OrderCutoffScheduler.IsWithinDeliveryWindow(BeforeCutoffUtc, BeforeCutoffUtc, windowDays: 7);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsWithinDeliveryWindow_ConfiguredThreeDayWindow_AtUpperBoundDPlus3_ReturnsTrue()
    {
        var dPlus3Utc = new DateTime(2026, 6, 20, 17, 0, 0, DateTimeKind.Utc);

        var result = OrderCutoffScheduler.IsWithinDeliveryWindow(BeforeCutoffUtc, dPlus3Utc, windowDays: 3);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsWithinDeliveryWindow_ConfiguredThreeDayWindow_BeyondDPlus3_ReturnsFalse()
    {
        var dPlus4Utc = new DateTime(2026, 6, 22, 17, 0, 0, DateTimeKind.Utc);

        var result = OrderCutoffScheduler.IsWithinDeliveryWindow(BeforeCutoffUtc, dPlus4Utc, windowDays: 3);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsWithinDeliveryWindow_ConfiguredFourteenDayWindow_AtDPlus10_ReturnsTrue()
    {
        var dPlus10Utc = new DateTime(2026, 6, 28, 17, 0, 0, DateTimeKind.Utc);

        var result = OrderCutoffScheduler.IsWithinDeliveryWindow(BeforeCutoffUtc, dPlus10Utc, windowDays: 14);

        result.Should().BeTrue();
    }

    [Fact]
    public void ResolveScheduledFor_CustomCutoffEarlierThanDefault_UsesCustomCutoff()
    {
        // BeforeCutoffUtc == 21:59 local — past a custom 21:00 cutoff even though it's before the
        // 22:00 default, proving the configured cutoff (SCRUM-355) is honored over the fallback.
        var result = OrderCutoffScheduler.ResolveScheduledFor(
            BeforeCutoffUtc, requestedScheduledFor: null, cutoffLocalTime: TimeSpan.FromHours(21));

        result.Should().Be(DPlus2Utc);
    }

    [Fact]
    public void ResolveScheduledFor_NullCutoff_FallsBackToDefault()
    {
        var result = OrderCutoffScheduler.ResolveScheduledFor(
            BeforeCutoffUtc, requestedScheduledFor: null, cutoffLocalTime: null);

        result.Should().Be(DPlus1Utc);
    }
}
