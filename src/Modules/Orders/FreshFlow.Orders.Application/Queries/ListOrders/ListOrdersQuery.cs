using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Queries;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Queries.ListOrders;

public sealed record ListOrdersQuery(
    Guid UserId,
    bool IsAdmin,
    Guid? RestaurantId,
    string? Status,
    DateTime? From,
    DateTime? To,
    string? Sort = OrderQueryParsing.DefaultSort,
    int Page = 1,
    int PageSize = 20) : IQuery<OrderListResponseDto>;
