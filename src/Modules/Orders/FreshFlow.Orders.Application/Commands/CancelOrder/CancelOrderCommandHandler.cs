using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.CancelOrder;

/// <summary>
/// Error precedence:
/// 404 order missing        → ORDER_NOT_FOUND
/// 403 not owner            → FORBIDDEN
/// 409 not cancellable      → ORDER_NOT_CANCELLABLE
/// refund/credit error      → propagated from credit service
/// 200 success
/// </summary>
internal sealed class CancelOrderCommandHandler(
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader,
    ICreditService creditService) : IRequestHandler<CancelOrderCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        OrderDto? cancelledOrder = null;
        var transaction = await orderRepository.ExecuteInSerializableTransactionAsync(async ct =>
        {
            var result = await CancelAsync(request, ct);
            if (result.IsFailure)
                return Result.Failure(result.Error);

            cancelledOrder = result.Value;
            return Result.Success();
        }, cancellationToken);

        return transaction.IsFailure
            ? Result<OrderDto>.Failure(transaction.Error)
            : Result<OrderDto>.Success(cancelledOrder!);
    }

    private async Task<Result<OrderDto>> CancelAsync(
        CancelOrderCommand request,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderDto>.Failure(Error.NotFound("ORDER", request.OrderId));

        if (!request.IsAdmin)
        {
            var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
            if (restaurant is null || restaurant.RestaurantId != order.RestaurantId)
                return Result<OrderDto>.Failure(
                    Error.Unauthorized("FORBIDDEN", "This order does not belong to the authenticated restaurant."));
        }

        var wasConfirmed = order.Status == OrderStatus.Confirmed;
        var cancelResult = order.Cancel(request.Reason);
        if (cancelResult.IsFailure)
            return Result<OrderDto>.Failure(cancelResult.Error);

        orderRepository.Track(order);

        if (wasConfirmed)
        {
            var reservations = order.Items
                .GroupBy(item => item.MarketProductId)
                .Select(group => new StockReservation(group.Key, group.Sum(item => item.Quantity)))
                .OrderBy(reservation => reservation.MarketProductId)
                .ToArray();

            if (!await orderRepository.ReleaseStockAsync(reservations, cancellationToken))
                return Result<OrderDto>.Failure(Error.Conflict(
                    "STOCK_RESERVATION_CONFLICT",
                    "The order stock reservation could not be released."));

            var refundResult = await creditService.RefundAsync(
                order.RestaurantId,
                order.Id,
                order.TotalAmount,
                "Order cancelled",
                cancellationToken);

            if (refundResult.IsFailure)
                return Result<OrderDto>.Failure(refundResult.Error);
        }

        return Result<OrderDto>.Success(OrderDtoMapper.ToDto(order));
    }
}
