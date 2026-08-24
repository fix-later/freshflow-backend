using FluentAssertions;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Orders.Domain.Events;

namespace FreshFlow.Orders.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class RestaurantCreditTests
{
    [Fact]
    public void Constructor_WithNegativeLimit_Throws()
    {
        var act = () => new RestaurantCredit(Guid.NewGuid(), creditLimit: -1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Charge_WithinLimit_IncreasesOutstandingBalance()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);

        credit.Charge(40m);

        credit.OutstandingBalance.Should().Be(40m);
        credit.AvailableCredit.Should().Be(60m);
    }

    [Fact]
    public void Charge_ExceedingLimit_Throws()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);

        var act = () => credit.Charge(101m);

        act.Should().Throw<InvalidOperationException>();
        credit.OutstandingBalance.Should().Be(0m);
    }

    [Fact]
    public void Settle_WithinOutstandingBalance_DecreasesBalance()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);
        credit.Charge(80m);

        credit.Settle(30m);

        credit.OutstandingBalance.Should().Be(50m);
        credit.AvailableCredit.Should().Be(50m);
    }

    [Fact]
    public void Settle_ExceedingOutstandingBalance_Throws()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);
        credit.Charge(20m);

        var act = () => credit.Settle(21m);

        act.Should().Throw<InvalidOperationException>();
        credit.OutstandingBalance.Should().Be(20m);
    }

    [Fact]
    public void Refund_WithinOutstandingBalance_DecreasesBalance()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);
        credit.Charge(70m);

        credit.Refund(25m);

        credit.OutstandingBalance.Should().Be(45m);
    }

    [Fact]
    public void Refund_ExceedingOutstandingBalance_GoesNegative()
    {
        // AUDIT-2026-08-23 C3: refunding past zero is valid — a negative balance means
        // FreshFlow owes the restaurant.
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);
        credit.Charge(20m);
        credit.Settle(20m);

        credit.Refund(30m);

        credit.OutstandingBalance.Should().Be(-30m);
    }

    [Fact]
    public void SetCreditLimit_NonNegativeValue_UpdatesLimit()
    {
        var credit = new RestaurantCredit(Guid.NewGuid());

        credit.SetCreditLimit(500m);

        credit.CreditLimit.Should().Be(500m);
    }

    [Fact]
    public void SetCreditLimit_Zero_AllowsRevokingCredit()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 500m);

        credit.SetCreditLimit(0m);

        credit.CreditLimit.Should().Be(0m);
    }

    [Fact]
    public void SetCreditLimit_NegativeValue_Throws()
    {
        var credit = new RestaurantCredit(Guid.NewGuid());

        var act = () => credit.SetCreditLimit(-1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SetCreditLimit_BelowOutstandingBalance_Throws()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);
        credit.Charge(80m);

        var act = () => credit.SetCreditLimit(50m);

        act.Should().Throw<InvalidOperationException>();
        credit.CreditLimit.Should().Be(100m);
    }

    // ── Threshold alerting (SCRUM-266) ──────────────────────────────────────────

    [Fact]
    public void Charge_UtilizationStaysBelowWarning_DoesNotRaiseEvent()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);

        credit.Charge(79m); // 79% — below the 80% warning threshold

        credit.DomainEvents.Should().BeEmpty();
        credit.LastAlertedLevel.Should().Be(CreditAlertLevel.None);
    }

    [Fact]
    public void Charge_CrossingIntoWarningThreshold_RaisesWarningEvent()
    {
        var restaurantId = Guid.NewGuid();
        var credit = new RestaurantCredit(restaurantId, creditLimit: 100m);

        credit.Charge(80m); // exactly 80% utilization

        credit.DomainEvents.Should().ContainSingle();
        var evt = credit.DomainEvents.Single().Should().BeOfType<CreditLimitThresholdReachedDomainEvent>().Subject;
        evt.RestaurantId.Should().Be(restaurantId);
        evt.Level.Should().Be(CreditAlertLevel.Warning);
        evt.Utilization.Should().Be(0.8m);
        evt.OutstandingBalance.Should().Be(80m);
        evt.CreditLimit.Should().Be(100m);
        credit.LastAlertedLevel.Should().Be(CreditAlertLevel.Warning);
    }

    [Fact]
    public void Charge_CrossingDirectlyIntoExceededThreshold_RaisesOnlyOneExceededEvent()
    {
        // A single charge jumping straight from None to Exceeded (skipping over Warning
        // in one step) must raise exactly ONE event — for the highest level reached, not
        // one per threshold passed through. (Charge() itself forbids overshooting past
        // 100% of the limit, so "Exceeded" is reached at exactly full utilization.)
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);

        credit.Charge(100m); // 100% utilization in one shot, straight from 0%

        credit.DomainEvents.Should().ContainSingle();
        credit.DomainEvents.Single().Should().BeOfType<CreditLimitThresholdReachedDomainEvent>()
            .Which.Level.Should().Be(CreditAlertLevel.Exceeded);
        credit.LastAlertedLevel.Should().Be(CreditAlertLevel.Exceeded);
    }

    [Fact]
    public void Charge_AlreadyAtWarningLevel_FurtherChargeStillWithinWarning_DoesNotReRaise()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);
        credit.Charge(80m); // crosses into Warning, raises + clears via ClearDomainEvents below
        credit.ClearDomainEvents();

        credit.Charge(10m); // now 90% — still Warning level, not yet Exceeded

        credit.DomainEvents.Should().BeEmpty("anti-spam: no re-emit while still at the same level");
        credit.LastAlertedLevel.Should().Be(CreditAlertLevel.Warning);
    }

    [Fact]
    public void Charge_FromWarningCrossingIntoExceeded_RaisesExceededEvent()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);
        credit.Charge(85m); // Warning
        credit.ClearDomainEvents();

        credit.Charge(15m); // now 100% — crosses into Exceeded

        credit.DomainEvents.Should().ContainSingle();
        credit.DomainEvents.Single().Should().BeOfType<CreditLimitThresholdReachedDomainEvent>()
            .Which.Level.Should().Be(CreditAlertLevel.Exceeded);
        credit.LastAlertedLevel.Should().Be(CreditAlertLevel.Exceeded);
    }

    [Fact]
    public void Settle_DroppingUtilizationBelowWarningThreshold_RearmsToNoneWithoutRaisingEvent()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);
        credit.Charge(90m); // Warning
        credit.ClearDomainEvents();

        credit.Settle(50m); // now 40% — back under Warning

        credit.DomainEvents.Should().BeEmpty("re-arming must never itself raise an event");
        credit.LastAlertedLevel.Should().Be(CreditAlertLevel.None);
    }

    [Fact]
    public void Refund_DroppingFromExceededToWarningRange_RearmsToWarningOnly()
    {
        // Re-arm is incremental per level, not a hard reset to None — dropping from
        // Exceeded to still-Warning-range utilization re-arms only down to Warning.
        // (Charge() forbids overshooting past 100% of the limit, so Exceeded is reached at
        // exactly full utilization.)
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);
        credit.Charge(100m); // Exceeded
        credit.ClearDomainEvents();

        credit.Refund(15m); // now 85% — Warning range, not below it

        credit.DomainEvents.Should().BeEmpty();
        credit.LastAlertedLevel.Should().Be(CreditAlertLevel.Warning);
    }

    [Fact]
    public void Charge_AfterRearmToNone_RecrossingWarningThreshold_RaisesEventAgain()
    {
        var credit = new RestaurantCredit(Guid.NewGuid(), creditLimit: 100m);
        credit.Charge(90m); // Warning
        credit.Settle(60m); // back to 30% — re-arms to None
        credit.ClearDomainEvents();

        credit.Charge(50m); // now 80% again — re-crosses Warning

        credit.DomainEvents.Should().ContainSingle();
        credit.DomainEvents.Single().Should().BeOfType<CreditLimitThresholdReachedDomainEvent>()
            .Which.Level.Should().Be(CreditAlertLevel.Warning);
        credit.LastAlertedLevel.Should().Be(CreditAlertLevel.Warning);
    }
}
