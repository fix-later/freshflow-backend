namespace FreshFlow.Orders.Domain.Enums;

/// <summary>
/// Tracks the order's position in the B2B credit (công nợ) lifecycle, independent of
/// <see cref="OrderStatus"/>. Replaces the legacy per-order payment-gateway semantics.
/// </summary>
public enum OrderPaymentStatus
{
    /// <summary>Order has not yet been confirmed; no debt has accrued.</summary>
    NotApplicable,

    /// <summary>Order confirmed; amount accrued as outstanding debt against the restaurant's credit limit.</summary>
    Outstanding,

    /// <summary>Outstanding debt fully settled by the restaurant.</summary>
    Settled,

    /// <summary>Order cancelled before settlement; no debt remains.</summary>
    Waived
}
