namespace FreshFlow.Auth.Domain.Enums;

/// <summary>
/// Lifecycle state of a restaurant account.
/// Stored in the DB as a lowercase text string: "pending" | "active" | "suspended".
/// </summary>
public enum RestaurantStatus
{
    /// <summary>Newly registered; awaiting admin approval.</summary>
    Pending = 0,

    /// <summary>Approved by admin; allowed to accept orders.</summary>
    Active = 1,

    /// <summary>Administratively suspended; cannot accept orders.</summary>
    Suspended = 2,
}
