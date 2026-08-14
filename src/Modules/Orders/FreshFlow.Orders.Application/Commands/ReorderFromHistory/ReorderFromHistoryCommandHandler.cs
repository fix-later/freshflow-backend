using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Services;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.ReorderFromHistory;

internal sealed class ReorderFromHistoryCommandHandler(
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader,
    IMarketProductReader marketProductReader,
    IOperationalSettingsRepository operationalSettings)
    : IRequestHandler<ReorderFromHistoryCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(
        ReorderFromHistoryCommand request, CancellationToken cancellationToken)
    {
        var sourceOrder = await orderRepository.FindByIdAsync(request.SourceOrderId, cancellationToken);
        if (sourceOrder is null)
            return Result<OrderDto>.Failure(Error.NotFound("ORDER", request.SourceOrderId));

        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null || restaurant.RestaurantId != sourceOrder.RestaurantId)
            return Result<OrderDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This order does not belong to the authenticated restaurant."));

        if (sourceOrder.Items.Count == 0)
            return Result<OrderDto>.Failure(Error.Validation(
                "ORDER_EMPTY", "Cannot reorder from an order with no items."));

        var settings = await operationalSettings.GetAsync(cancellationToken);
        if (request.ScheduledFor is not null
            && !OrderCutoffScheduler.IsWithinDeliveryWindow(DateTime.UtcNow, request.ScheduledFor.Value, settings.DeliveryWindowDays))
        {
            return Result<OrderDto>.Failure(Error.Validation(
                "DELIVERY_DATE_OUT_OF_WINDOW",
                $"Delivery date must be within the next {settings.DeliveryWindowDays} days and not in the past."));
        }

        var snapshots = new Dictionary<Guid, MarketProductSnapshotDto>();
        foreach (var group in sourceOrder.Items.GroupBy(i => i.MarketProductId))
        {
            var snapshot = await marketProductReader.FindAsync(group.Key, cancellationToken);
            if (snapshot is null)
                return Result<OrderDto>.Failure(Error.Validation(
                    "INVALID_PRODUCT", $"Product '{group.Key}' is not available."));

            var requestedQuantity = group.Sum(i => i.Quantity);
            if (requestedQuantity > snapshot.AvailableQuantity)
                return Result<OrderDto>.Failure(Error.Validation(
                    "INSUFFICIENT_STOCK",
                    $"Requested quantity {requestedQuantity} exceeds available stock " +
                    $"{snapshot.AvailableQuantity} for product '{group.Key}'."));

            snapshots[group.Key] = snapshot;
        }

        var newOrder = new Order(
            sourceOrder.RestaurantId,
            request.ScheduledFor,
            request.Notes ?? sourceOrder.Notes);

        foreach (var sourceItem in sourceOrder.Items)
        {
            var snapshot = snapshots[sourceItem.MarketProductId];
            var addResult = newOrder.AddItem(
                snapshot.MarketProductId,
                snapshot.ProductName,
                sourceItem.Quantity,
                snapshot.CurrentPrice,
                snapshot.MarketId,
                snapshot.PackingCode);

            if (addResult.IsFailure)
                return Result<OrderDto>.Failure(addResult.Error);
        }

        await orderRepository.AddAsync(newOrder, cancellationToken);
        await orderRepository.SaveChangesAsync(cancellationToken);

        return Result<OrderDto>.Success(OrderDtoMapper.ToDto(newOrder));
    }
}
