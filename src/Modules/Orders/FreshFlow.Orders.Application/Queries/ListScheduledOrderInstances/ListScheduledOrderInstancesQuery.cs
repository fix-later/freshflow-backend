using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Queries.ListScheduledOrderInstances;

public sealed record ListScheduledOrderInstancesQuery(
    Guid UserId,
    bool IsAdmin,
    Guid ScheduledOrderId,
    int Page = 1,
    int PageSize = 20) : IQuery<OrderListResponseDto>;
