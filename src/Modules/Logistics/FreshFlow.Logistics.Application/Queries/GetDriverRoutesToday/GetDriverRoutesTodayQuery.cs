using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Queries.GetDriverRoutesToday;

public sealed record GetDriverRoutesTodayQuery(Guid DriverUserId)
    : IQuery<IReadOnlyList<DriverRouteDto>>;
