using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.RemoveOrderItem;

/// <summary>
/// SCRUM-189 — UC-ORD-04: Remove Item from Draft Order.
///
/// Error precedence:
/// 404 order missing   → ORDER_NOT_FOUND
/// 403 not owner        → FORBIDDEN
/// 409 order not draft  → ORDER_NOT_DRAFT (from domain)
/// 404 item missing     → ORDER_ITEM_NOT_FOUND (from domain)
/// 200 success
/// </summary>
internal sealed class RemoveOrderItemCommandHandler(
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader)
    : IRequestHandler<RemoveOrderItemCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(RemoveOrderItemCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderDto>.Failure(Error.NotFound("ORDER", request.OrderId));

        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null || restaurant.RestaurantId != order.RestaurantId)
            return Result<OrderDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This order does not belong to the authenticated restaurant."));

        var removeResult = order.RemoveItem(request.OrderItemId);
        if (removeResult.IsFailure)
            return Result<OrderDto>.Failure(removeResult.Error);

        orderRepository.Track(order);
        await orderRepository.SaveChangesAsync(cancellationToken);

        return Result<OrderDto>.Success(OrderDtoMapper.ToDto(order));
    }
}
