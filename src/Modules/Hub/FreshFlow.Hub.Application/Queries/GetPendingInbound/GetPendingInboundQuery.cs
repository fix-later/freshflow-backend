using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Queries.GetPendingInbound;

public sealed record GetPendingInboundQuery(
    Guid HubId,
    string? Cursor = null,
    int PageSize = 50) : IQuery<HubInboundPageDto>;
