using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Queries.GetSortingProgress;

public sealed record GetSortingProgressQuery(
    Guid HubId,
    Guid RouteId,
    Guid ActorUserId = default,
    bool BypassHubAssignment = false)
    : IQuery<IReadOnlyList<HubSortingProgressDto>>, IHubAccessRequest;
