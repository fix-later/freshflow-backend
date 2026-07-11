using FreshFlow.Hub.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Hub.Application.Queries.ListOutbound;

public sealed record ListOutboundQuery(
    Guid HubId,
    DateOnly? Date = null,
    string? Cursor = null,
    int PageSize = 50) : IQuery<HubOutboundPageDto>;
