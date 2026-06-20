using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Queries.ListScheduledOrderInstances;

internal sealed class ListScheduledOrderInstancesQueryHandler(
    IScheduledOrderRepository scheduledOrderRepository,
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader)
    : IRequestHandler<ListScheduledOrderInstancesQuery, Result<OrderListResponseDto>>
{
    public async Task<Result<OrderListResponseDto>> Handle(
        ListScheduledOrderInstancesQuery request, CancellationToken cancellationToken)
    {
        var scheduledOrder = await scheduledOrderRepository.FindByIdAsync(request.ScheduledOrderId, cancellationToken);
        if (scheduledOrder is null)
            return Result<OrderListResponseDto>.Failure(Error.NotFound("SCHEDULED_ORDER", request.ScheduledOrderId));

        if (!request.IsAdmin)
        {
            var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
            if (restaurant is null || restaurant.RestaurantId != scheduledOrder.RestaurantId)
                return Result<OrderListResponseDto>.Failure(
                    Error.Unauthorized("FORBIDDEN", "This recurring schedule is not accessible."));
        }

        var (orders, total) = await orderRepository.GetByScheduledOrderIdAsync(
            scheduledOrder.Id, request.Page, request.PageSize, cancellationToken);

        var response = new OrderListResponseDto(
            orders.Select(OrderDtoMapper.ToListItemDto).ToList(),
            new OrderPaginationMeta(total, request.Page, request.PageSize));

        return Result<OrderListResponseDto>.Success(response);
    }
}
