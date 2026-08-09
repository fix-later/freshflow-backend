using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Queries.GetRoutePlan;

public sealed record GetRoutePlanQuery(Guid PlanId) : IQuery<RoutePlanDto>;
