using FreshFlow.Orders.Domain.Enums;
using FreshFlow.Orders.Domain.Events;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Orders.Domain.Entities;

/// <summary>
/// Aggregate root (DEC-CRE-04, SCRUM-266) — became one specifically to raise
/// <see cref="CreditLimitThresholdReachedDomainEvent"/> from <see cref="Charge"/> when a
/// charge crosses a not-yet-alerted utilization threshold. <see cref="LastAlertedLevel"/>
/// is the anti-spam state: an upward crossing only raises while the new level is above it;
/// <see cref="Settle"/>/<see cref="Refund"/> re-arm it (lower it, never raise it) when
/// utilization drops back below it, without raising an event themselves.
/// </summary>
/// <remarks>
/// AUDIT-2026-08-23 C3: <see cref="OutstandingBalance"/> may go negative. A negative balance
/// means FreshFlow owes the restaurant (e.g. a refund issued after the account was already
/// settled to zero); it is carried forward and offset against future charges.
/// </remarks>
public sealed class RestaurantCredit : AggregateRoot
{
    private const decimal WarningUtilization = 0.8m;
    private const decimal ExceededUtilization = 1.0m;

    private RestaurantCredit() { } // EF Core

    public RestaurantCredit(Guid restaurantId, decimal creditLimit = 0m)
    {
        if (restaurantId == Guid.Empty)
            throw new ArgumentException("Restaurant id is required.", nameof(restaurantId));

        if (creditLimit < 0m)
            throw new ArgumentOutOfRangeException(nameof(creditLimit), creditLimit, "Credit limit must be non-negative.");

        RestaurantId = restaurantId;
        CreditLimit = creditLimit;
        OutstandingBalance = 0m;
        LastAlertedLevel = CreditAlertLevel.None;
    }

    public Guid RestaurantId { get; private set; }
    public decimal CreditLimit { get; private set; }
    public decimal OutstandingBalance { get; private set; }
    public CreditAlertLevel LastAlertedLevel { get; private set; }

    public decimal AvailableCredit => CreditLimit - OutstandingBalance;

    public bool CanCharge(decimal amount) =>
        amount > 0m && OutstandingBalance + amount <= CreditLimit;

    public void Charge(decimal amount)
    {
        EnsurePositive(amount);

        if (OutstandingBalance + amount > CreditLimit)
            throw new InvalidOperationException("Charge would exceed the restaurant credit limit.");

        OutstandingBalance += amount;
        Touch();
        RaiseThresholdEventIfCrossed();
    }

    public void Settle(decimal amount)
    {
        EnsurePositive(amount);

        // ponytail: ceiling is intentional — you can't record a payment against an account
        // already in credit (OutstandingBalance <= 0). A restaurant in credit that stops
        // ordering must be refunded out of band (bank transfer); there is no cash-out path in v1.
        if (amount > OutstandingBalance)
            throw new InvalidOperationException("Settlement amount cannot exceed outstanding balance.");

        OutstandingBalance -= amount;
        Touch();
        RearmAlertIfBelowThreshold();
    }

    public void Refund(decimal amount)
    {
        EnsurePositive(amount);

        // AUDIT-2026-08-23 C3: no ceiling here — a refund may exceed the currently outstanding
        // balance (e.g. account already settled to zero). OutstandingBalance goes negative,
        // meaning FreshFlow owes the restaurant.
        OutstandingBalance -= amount;
        Touch();
        RearmAlertIfBelowThreshold();
    }

    public void SetCreditLimit(decimal newLimit)
    {
        if (newLimit < 0m)
            throw new ArgumentOutOfRangeException(nameof(newLimit), newLimit, "Credit limit must be non-negative.");

        if (newLimit < OutstandingBalance)
            throw new InvalidOperationException("Credit limit cannot be set below the outstanding balance.");

        CreditLimit = newLimit;
        Touch();
    }

    private static void EnsurePositive(decimal amount)
    {
        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be greater than zero.");
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;

    // ── Threshold alerting (SCRUM-266) ──────────────────────────────────────────

    /// <summary>
    /// Warning = [80%, 100%) utilization, Exceeded = &gt;=100%. Guards CreditLimit &lt;= 0
    /// to avoid a decimal divide-by-zero — an account with no credit limit is always at
    /// level None (it can never carry a positive balance: <see cref="Charge"/> would have
    /// already rejected any charge against a zero limit).
    /// </summary>
    private CreditAlertLevel CurrentAlertLevel()
    {
        if (CreditLimit <= 0m)
            return CreditAlertLevel.None;

        var utilization = OutstandingBalance / CreditLimit;
        if (utilization >= ExceededUtilization)
            return CreditAlertLevel.Exceeded;
        if (utilization >= WarningUtilization)
            return CreditAlertLevel.Warning;
        return CreditAlertLevel.None;
    }

    private void RaiseThresholdEventIfCrossed()
    {
        var level = CurrentAlertLevel();
        if (level <= LastAlertedLevel)
            return; // anti-spam: not a NEW crossing

        var utilization = CreditLimit > 0m ? OutstandingBalance / CreditLimit : 0m;
        RaiseDomainEvent(new CreditLimitThresholdReachedDomainEvent(
            RestaurantId, level, utilization, OutstandingBalance, CreditLimit, DateTime.UtcNow));
        LastAlertedLevel = level;
    }

    private void RearmAlertIfBelowThreshold()
    {
        var level = CurrentAlertLevel();
        if (level < LastAlertedLevel)
            LastAlertedLevel = level; // re-arm — never raises an event on the way down
    }
}
