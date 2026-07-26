using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Queries.GetHubProcurementPlan;

public sealed record GetHubProcurementPlanQuery(
    Guid HubId,
    DateOnly Date,
    Guid ActorUserId,
    bool BypassHubAssignment)
    : IQuery<HubProcurementPlanDto>, IHubAccessRequest;
