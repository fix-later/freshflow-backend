using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.UpdateOrderItem;

/// <summary>
/// SCRUM-186 — UC-ORD-03: Update Draft Order Item.
///
/// Error precedence:
/// 404 order missing        → ORDER_NOT_FOUND
/// 403 not owner             → FORBIDDEN
/// 404 item missing          → ORDER_ITEM_NOT_FOUND (from domain)
/// 422 invalid product       → INVALID_PRODUCT
/// 422 insufficient stock    → INSUFFICIENT_STOCK (re-checked against live availability)
/// 409 order not draft       → ORDER_NOT_DRAFT (from domain)
/// 200 success
/// </summary>
internal sealed class UpdateOrderItemCommandHandler(
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader,
    IMarketProductReader marketProductReader)
    : IRequestHandler<UpdateOrderItemCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(UpdateOrderItemCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderDto>.Failure(Error.NotFound("ORDER", request.OrderId));

        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null || restaurant.RestaurantId != order.RestaurantId)
            return Result<OrderDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This order does not belong to the authenticated restaurant."));

        var item = order.Items.FirstOrDefault(i => i.Id == request.OrderItemId);
        if (item is null)
            return Result<OrderDto>.Failure(Error.NotFound("ORDER_ITEM", request.OrderItemId));

        var snapshot = await marketProductReader.FindAsync(item.MarketProductId, cancellationToken);
        if (snapshot is null)
            return Result<OrderDto>.Failure(Error.Validation(
                "INVALID_PRODUCT", $"Product '{item.MarketProductId}' is not available."));

        var requestedTotalQuantity = order.Items
            .Where(i => i.MarketProductId == item.MarketProductId && i.Id != item.Id)
            .Sum(i => i.Quantity) + request.Quantity;

        if (requestedTotalQuantity > snapshot.AvailableQuantity)
            return Result<OrderDto>.Failure(Error.Validation(
                "INSUFFICIENT_STOCK",
                $"Requested quantity {requestedTotalQuantity} exceeds available stock " +
                $"{snapshot.AvailableQuantity} for product '{item.MarketProductId}'."));

        var updateResult = order.UpdateItem(request.OrderItemId, request.Quantity);
        if (updateResult.IsFailure)
            return Result<OrderDto>.Failure(updateResult.Error);

        orderRepository.Track(order);
        await orderRepository.SaveChangesAsync(cancellationToken);

        return Result<OrderDto>.Success(OrderDtoMapper.ToDto(order));
    }
}
