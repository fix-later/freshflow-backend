using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Services;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.ConfirmOrder;

internal sealed class ConfirmOrderCommandHandler(
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader,
    ICreditService creditService)
    : IRequestHandler<ConfirmOrderCommand, Result<OrderDto>>
{
    public Task<Result<OrderDto>> Handle(ConfirmOrderCommand request, CancellationToken cancellationToken) =>
        Handle(request, cancellationToken, DateTime.UtcNow);

    internal async Task<Result<OrderDto>> Handle(
        ConfirmOrderCommand request, CancellationToken cancellationToken, DateTime confirmedAtUtc)
    {
        var order = await orderRepository.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderDto>.Failure(Error.NotFound("ORDER", request.OrderId));

        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null || restaurant.RestaurantId != order.RestaurantId)
            return Result<OrderDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This order does not belong to the authenticated restaurant."));

        var canConfirmResult = order.CanConfirm();
        if (canConfirmResult.IsFailure)
            return Result<OrderDto>.Failure(canConfirmResult.Error);

        var canChargeResult = await creditService.CanChargeAsync(
            order.RestaurantId, order.TotalAmount, cancellationToken);
        if (canChargeResult.IsFailure)
            return Result<OrderDto>.Failure(canChargeResult.Error);

        var evaluation = OrderConfirmationEvaluator.Evaluate(order, canChargeResult.Value, confirmedAtUtc);
        if (evaluation.Issues.Count > 0)
            return Result<OrderDto>.Failure(evaluation.Issues[0]);

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
