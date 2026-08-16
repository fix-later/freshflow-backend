namespace FreshFlow.Logistics.Application.Dtos;

public sealed record EligibilityResultDto(
    bool IsEligible,
    IReadOnlyList<string> Reasons,
    decimal RouteLoadKg = 0m,
    decimal VehicleCapacityKg = 0m,
    bool IsWeightComplete = true);
