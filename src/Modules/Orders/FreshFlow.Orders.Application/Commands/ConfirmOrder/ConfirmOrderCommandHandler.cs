using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Enums;
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
        var source = await orderRepository.FindConfirmationSourceAsync(request.OrderId, cancellationToken);
        if (source is null)
            return Result<OrderDto>.Failure(Error.NotFound("ORDER", request.OrderId));

        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null || restaurant.RestaurantId != source.RestaurantId)
            return Result<OrderDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This order does not belong to the authenticated restaurant."));
        if (!restaurant.IsApproved)
            return Result<OrderDto>.Failure(
                Error.Unauthorized("RESTAURANT_NOT_ACTIVE", "A suspended or unapproved restaurant cannot confirm orders."));

        if (source.Status != OrderStatus.Draft)
            return Result<OrderDto>.Failure(Error.Conflict(
                "ORDER_NOT_DRAFT", "Only a draft order can be confirmed."));
        if (source.MarketProductIds.Count == 0)
            return Result<OrderDto>.Failure(Error.Validation(
                "ORDER_EMPTY", "Cannot confirm an order with no items."));

        var roadDistance = await confirmationService.GetRoadDistanceAsync(
            source.MarketProductIds,
            source.RestaurantId,
            request.DeliveryAddressId,
            cancellationToken);
        if (roadDistance.IsFailure)
            return Result<OrderDto>.Failure(roadDistance.Error);

        OrderDto? confirmedOrder = null;
        var transaction = await orderRepository.ExecuteInSerializableTransactionAsync(async ct =>
        {
            var result = await ConfirmAsync(request, roadDistance.Value, confirmedAtUtc, ct);
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
        ConfirmOrderCommand request,
        RoadDistanceResult roadDistance,
        DateTime confirmedAtUtc,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderDto>.Failure(Error.NotFound("ORDER", request.OrderId));

        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null || restaurant.RestaurantId != order.RestaurantId)
            return Result<OrderDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This order does not belong to the authenticated restaurant."));
        if (!restaurant.IsApproved)
            return Result<OrderDto>.Failure(
                Error.Unauthorized("RESTAURANT_NOT_ACTIVE", "A suspended or unapproved restaurant cannot confirm orders."));

        return await confirmationService.ConfirmAsync(
            order,
            order.RestaurantId,
            request.DeliveryAddressId,
            roadDistance,
            confirmedAtUtc,
            cancellationToken);
    }
}
