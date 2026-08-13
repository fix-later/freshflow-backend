using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.AddOrderItem;

/// <summary>
/// SCRUM-183 — UC-ORD-02: Add Item to Draft Order.
///
/// Error precedence:
/// 404 order missing           → ORDER_NOT_FOUND
/// 403 not owner                → FORBIDDEN
/// 422 invalid product          → INVALID_PRODUCT
/// 422 insufficient stock       → INSUFFICIENT_STOCK
/// 409 order not draft          → ORDER_NOT_DRAFT (from domain)
/// 200 success
/// </summary>
internal sealed class AddOrderItemCommandHandler(
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader,
    IMarketProductReader marketProductReader)
    : IRequestHandler<AddOrderItemCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(AddOrderItemCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderDto>.Failure(Error.NotFound("ORDER", request.OrderId));

        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null || restaurant.RestaurantId != order.RestaurantId)
            return Result<OrderDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This order does not belong to the authenticated restaurant."));

        var snapshot = await marketProductReader.FindAsync(request.MarketProductId, cancellationToken);
        if (snapshot is null)
            return Result<OrderDto>.Failure(Error.Validation(
                "INVALID_PRODUCT", $"Product '{request.MarketProductId}' is not available."));

        var requestedTotalQuantity = order.Items
            .Where(i => i.MarketProductId == request.MarketProductId)
            .Sum(i => i.Quantity) + request.Quantity;

        if (requestedTotalQuantity > snapshot.AvailableQuantity)
            return Result<OrderDto>.Failure(Error.Validation(
                "INSUFFICIENT_STOCK",
                $"Requested quantity {requestedTotalQuantity} exceeds available stock " +
                $"{snapshot.AvailableQuantity} for product '{request.MarketProductId}'."));

        var existingItemIds = order.Items.Select(i => i.Id).ToHashSet();
        var addResult = order.AddItem(
            snapshot.MarketProductId,
            snapshot.ProductName,
            request.Quantity,
            snapshot.CurrentPrice,
            snapshot.MarketId);
        if (addResult.IsFailure)
            return Result<OrderDto>.Failure(addResult.Error);

        var newItem = order.Items.Single(i => !existingItemIds.Contains(i.Id));
        orderRepository.TrackNewItem(newItem);
        orderRepository.Track(order);
        await orderRepository.SaveChangesAsync(cancellationToken);

        return Result<OrderDto>.Success(OrderDtoMapper.ToDto(order));
    }
}
