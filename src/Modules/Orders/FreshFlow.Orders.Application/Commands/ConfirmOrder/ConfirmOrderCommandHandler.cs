using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Services;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.ConfirmOrder;

internal sealed class ConfirmOrderCommandHandler(
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader,
    ICreditService creditService,
    IOperationalSettingsRepository operationalSettings)
    : IRequestHandler<ConfirmOrderCommand, Result<OrderDto>>
{
    public Task<Result<OrderDto>> Handle(ConfirmOrderCommand request, CancellationToken cancellationToken) =>
        Handle(request, cancellationToken, DateTime.UtcNow);

    internal async Task<Result<OrderDto>> Handle(
        ConfirmOrderCommand request, CancellationToken cancellationToken, DateTime confirmedAtUtc)
    {
        OrderDto? confirmedOrder = null;
        var transaction = await orderRepository.ExecuteInSerializableTransactionAsync(async ct =>
        {
            var result = await ConfirmAsync(request, ct, confirmedAtUtc);
            if (result.IsFailure)
                return Result.Failure(result.Error);

            confirmedOrder = result.Value;
            return Result.Success();
        }, cancellationToken);

        return transaction.IsFailure
            ? Result<OrderDto>.Failure(transaction.Error)
            : Result<OrderDto>.Success(confirmedOrder!);
    }

    private async Task<Result<OrderDto>> ConfirmAsync(
        ConfirmOrderCommand request, CancellationToken cancellationToken, DateTime confirmedAtUtc)
    {
        var order = await orderRepository.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderDto>.Failure(Error.NotFound("ORDER", request.OrderId));

        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null || restaurant.RestaurantId != order.RestaurantId)
            return Result<OrderDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This order does not belong to the authenticated restaurant."));

        // A suspended (or not-yet-approved) restaurant must not place orders, even on drafts that
        // existed before suspension. IsApproved is true only while the restaurant status is active.
        if (!restaurant.IsApproved)
            return Result<OrderDto>.Failure(
                Error.Unauthorized("RESTAURANT_NOT_ACTIVE", "A suspended or unapproved restaurant cannot confirm orders."));

        var canConfirmResult = order.CanConfirm();
        if (canConfirmResult.IsFailure)
            return Result<OrderDto>.Failure(canConfirmResult.Error);

        var canChargeResult = await creditService.CanChargeAsync(
            order.RestaurantId, order.TotalAmount, cancellationToken);
        if (canChargeResult.IsFailure)
            return Result<OrderDto>.Failure(canChargeResult.Error);

        var settings = await operationalSettings.GetAsync(cancellationToken);
        var evaluation = OrderConfirmationEvaluator.Evaluate(
            order, canChargeResult.Value, confirmedAtUtc, settings.DeliveryWindowDays, settings.DailyCutoffTime.ToTimeSpan());
        if (evaluation.Issues.Count > 0)
            return Result<OrderDto>.Failure(evaluation.Issues[0]);

        var reservations = order.Items
            .GroupBy(item => item.MarketProductId)
            .Select(group => new StockReservation(group.Key, group.Sum(item => item.Quantity)))
            .OrderBy(reservation => reservation.MarketProductId)
            .ToArray();

        if (!await orderRepository.TryReserveStockAsync(reservations, cancellationToken))
            return Result<OrderDto>.Failure(Error.Validation(
                "INSUFFICIENT_STOCK",
                "One or more products no longer have enough available stock."));

        var rescheduledFor = evaluation.ResolvedScheduledFor;
        if (rescheduledFor != order.ScheduledFor && rescheduledFor is not null)
        {
            var rescheduleResult = order.RescheduleFor(rescheduledFor.Value);
            if (rescheduleResult.IsFailure)
                return Result<OrderDto>.Failure(rescheduleResult.Error);
        }

        var confirmResult = order.Confirm();
        if (confirmResult.IsFailure)
            return Result<OrderDto>.Failure(confirmResult.Error);

        orderRepository.Track(order);

        var chargeResult = await creditService.ChargeAsync(
            order.RestaurantId, order.Id, order.TotalAmount, "Order confirmed", cancellationToken);
        if (chargeResult.IsFailure)
            return Result<OrderDto>.Failure(chargeResult.Error);

        // Seam for SCRUM-266 (credit-limit alert): chargeResult.Value already exposes the
        // post-charge CreditLimit/OutstandingBalance/AvailableCredit for this restaurant, so a
        // future threshold check (and its integration event to Notifications) can hook in right
        // here without touching CreditService or the order-confirmation flow above.

        return Result<OrderDto>.Success(OrderDtoMapper.ToDto(order));
    }
}
