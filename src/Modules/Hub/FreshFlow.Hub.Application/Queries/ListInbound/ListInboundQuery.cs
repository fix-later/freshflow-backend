using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Queries.ListInbound;

public sealed record ListInboundQuery(
    Guid HubId,
    DateOnly? Date = null,
    string? Cursor = null,
    int PageSize = 50) : IQuery<HubInboundPageDto>;
