using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Queries.GetOrder;

public sealed record GetOrderQuery(Guid UserId, bool IsAdmin, Guid OrderId) : IQuery<OrderDto>;
