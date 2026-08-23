namespace FreshFlow.Logistics.Domain.ValueObjects;

public sealed record RoutePlanUnassigned(
    Guid RestaurantId,
    string RestaurantName,
    IReadOnlyList<Guid> OrderIds,
    decimal LoadKg,
    string Reason,
    // M7: distinguishes "didn't fit" (solver, default, blocks approval) from "excluded for
    // incomplete data" (builder, does not block approval). Default false so existing jsonb rows
    // with no such field keep blocking approval exactly as before.
    bool ExcludedForIncompleteData = false);
