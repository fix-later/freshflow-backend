using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.ConfirmOrder;

internal sealed class ConfirmOrderCommandHandler(
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader,
    IOrderConfirmationService confirmationService)
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

        return await confirmationService.ConfirmAsync(
            order, order.RestaurantId, request.DeliveryAddressId, confirmedAtUtc, cancellationToken);
    }
}
