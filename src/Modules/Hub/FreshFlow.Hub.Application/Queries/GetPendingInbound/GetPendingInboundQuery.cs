using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Queries.GetPendingInbound;

public sealed record GetPendingInboundQuery(
    Guid HubId,
    string? Cursor = null,
    int PageSize = 50,
    Guid ActorUserId = default,
    bool BypassHubAssignment = false)
    : IQuery<HubInboundPageDto>, IHubAccessRequest;
