using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Queries.GetInboundLabels;

public sealed record GetInboundLabelsQuery(
    Guid HubId,
    Guid InboundId,
    Guid ActorUserId = default,
    bool BypassHubAssignment = false)
    : IQuery<HubInboundLabelsDto>, IHubAccessRequest;
