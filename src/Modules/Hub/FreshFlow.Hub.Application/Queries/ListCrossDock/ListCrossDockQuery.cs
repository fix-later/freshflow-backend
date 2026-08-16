using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Queries.ListCrossDock;

public sealed record ListCrossDockQuery(
    Guid HubId,
    string? Status = null,
    string? Cursor = null,
    int PageSize = 50,
    Guid ActorUserId = default,
    bool BypassHubAssignment = false)
    : IQuery<CrossDockTransferPageDto>, IHubAccessRequest;
