using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Queries.GetOrder;

internal sealed class GetOrderQueryHandler(
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader) : IRequestHandler<GetOrderQuery, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderDto>.Failure(Error.NotFound("ORDER", request.OrderId));

        if (!request.IsAdmin)
        {
            var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
            if (restaurant is null || restaurant.RestaurantId != order.RestaurantId)
                return Result<OrderDto>.Failure(
                    Error.Unauthorized("FORBIDDEN", "This order is not accessible."));
        }

        return Result<OrderDto>.Success(OrderDtoMapper.ToDto(order));
    }
}
