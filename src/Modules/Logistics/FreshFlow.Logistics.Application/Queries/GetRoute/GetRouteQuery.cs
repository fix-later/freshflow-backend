using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Queries.GetRoute;

public sealed record GetRouteQuery(Guid Id) : IQuery<RouteDto>;
