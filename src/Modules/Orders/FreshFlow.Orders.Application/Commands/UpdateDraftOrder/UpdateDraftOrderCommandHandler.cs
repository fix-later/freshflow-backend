using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Services;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.UpdateDraftOrder;

/// <summary>
/// Error precedence:
/// 404 order missing         → ORDER_NOT_FOUND
/// 403 not owner              → FORBIDDEN
/// 422 delivery date invalid  → DELIVERY_DATE_OUT_OF_WINDOW (SCRUM-196, same rule as order creation)
/// 409 order not draft        → ORDER_NOT_DRAFT (from domain)
/// 200 success
/// </summary>
internal sealed class UpdateDraftOrderCommandHandler(
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader,
    IOperationalSettingsRepository operationalSettings)
    : IRequestHandler<UpdateDraftOrderCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(UpdateDraftOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderDto>.Failure(Error.NotFound("ORDER", request.OrderId));

        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null || restaurant.RestaurantId != order.RestaurantId)
            return Result<OrderDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This order does not belong to the authenticated restaurant."));

        if (request.ScheduledFor is not null)
        {
            var settings = await operationalSettings.GetAsync(cancellationToken);
            if (!OrderCutoffScheduler.IsWithinDeliveryWindow(
                    DateTime.UtcNow, request.ScheduledFor.Value, settings.DeliveryWindowDays))
            {
                return Result<OrderDto>.Failure(Error.Validation(
                    "DELIVERY_DATE_OUT_OF_WINDOW",
                    $"Delivery date must be within the next {settings.DeliveryWindowDays} days and not in the past."));
            }
        }

        var updateNotesResult = order.UpdateNotes(request.Notes);
        if (updateNotesResult.IsFailure)
            return Result<OrderDto>.Failure(updateNotesResult.Error);

        var updateScheduleResult = order.UpdateScheduledFor(request.ScheduledFor);
        if (updateScheduleResult.IsFailure)
            return Result<OrderDto>.Failure(updateScheduleResult.Error);

        orderRepository.Track(order);
        await orderRepository.SaveChangesAsync(cancellationToken);

        return Result<OrderDto>.Success(OrderDtoMapper.ToDto(order));
    }
}
