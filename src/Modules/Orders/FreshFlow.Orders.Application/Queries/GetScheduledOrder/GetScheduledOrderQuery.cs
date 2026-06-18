using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Queries.GetScheduledOrder;

public sealed record GetScheduledOrderQuery(
    Guid UserId,
    bool IsAdmin,
    Guid ScheduledOrderId) : IQuery<ScheduledOrderDto>;
