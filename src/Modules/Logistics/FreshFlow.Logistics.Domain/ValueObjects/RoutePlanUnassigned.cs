namespace FreshFlow.Logistics.Domain.ValueObjects;

public sealed record RoutePlanUnassigned(
    Guid RestaurantId,
    string RestaurantName,
    IReadOnlyList<Guid> OrderIds,
    decimal LoadKg,
    string Reason);
