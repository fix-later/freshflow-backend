using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Orders.Application.EventHandlers;

/// <summary>
/// Handles <see cref="OrderStatusChangedDomainEvent"/> by broadcasting the committed status change
/// to SignalR clients via <see cref="IOrderBroadcastService"/>.
///
/// Dispatch contract:
///   - This handler runs post-commit because AppDbContext dispatches domain events after
///     SaveChanges succeeds, so clients never observe a status that was rolled back.
///   - Broadcast failures are logged and swallowed; realtime must not fail the already
///     committed order transition.
/// </summary>
internal sealed class OrderStatusChangedDomainEventHandler(
    IOrderBroadcastService broadcastService,
    ILogger<OrderStatusChangedDomainEventHandler> logger)
    : INotificationHandler<OrderStatusChangedDomainEvent>
{
    public async Task Handle(
        OrderStatusChangedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            var dto = new OrderStatusChangedBroadcastDto(
                notification.OrderId,
                notification.RestaurantId,
                OrderDtoMapper.ToApiStatus(notification.PreviousStatus),
                OrderDtoMapper.ToApiStatus(notification.NewStatus),
                notification.OccurredAt,
                EstimatedDeliveryAt: null);

            await broadcastService.BroadcastStatusChangedAsync(dto, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to broadcast order status change for OrderId={OrderId} RestaurantId={RestaurantId}",
                notification.OrderId,
                notification.RestaurantId);
        }
    }
}
