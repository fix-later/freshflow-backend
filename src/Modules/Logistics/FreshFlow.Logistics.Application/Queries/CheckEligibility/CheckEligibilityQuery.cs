using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Queries.CheckEligibility;

public sealed record CheckEligibilityQuery(
    Guid RouteId,
    Guid VehicleId,
    Guid? DriverUserId) : IQuery<EligibilityResultDto>;
