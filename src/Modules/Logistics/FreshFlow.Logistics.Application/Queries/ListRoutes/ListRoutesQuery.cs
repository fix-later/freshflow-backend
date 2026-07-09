using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Queries.ListRoutes;

public sealed record ListRoutesQuery(
    string? Cursor,
    int PageSize = 50,
    DateOnly? ServiceDate = null,
    string? Status = null) : IQuery<RoutePageDto>;
