using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Queries.ListScheduledOrders;

public sealed record ListScheduledOrdersQuery(
    Guid UserId,
    bool IsAdmin,
    Guid? RestaurantId,
    bool IncludeCancelled = false,
    int Page = 1,
    int PageSize = 20) : IQuery<ScheduledOrderListResponseDto>;
