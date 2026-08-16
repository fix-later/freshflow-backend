using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Queries.ListHandovers;

public sealed record ListHandoversQuery(
    Guid HubId,
    string? Cursor = null,
    int PageSize = 50,
    Guid ActorUserId = default,
    bool BypassHubAssignment = false)
    : IQuery<HubHandoverPageDto>, IHubAccessRequest;
