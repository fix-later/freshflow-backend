using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Services;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.UpdateScheduledOrder;

internal sealed class UpdateScheduledOrderCommandHandler(
    IScheduledOrderRepository scheduledOrderRepository,
    IRestaurantReader restaurantReader,
    IMarketProductReader? marketProductReader = null)
    : IRequestHandler<UpdateScheduledOrderCommand, Result<ScheduledOrderDto>>
{
    public async Task<Result<ScheduledOrderDto>> Handle(
        UpdateScheduledOrderCommand request, CancellationToken cancellationToken)
    {
        var scheduledOrder = await scheduledOrderRepository.FindByIdAsync(request.ScheduledOrderId, cancellationToken);
        if (scheduledOrder is null)
            return Result<ScheduledOrderDto>.Failure(Error.NotFound("SCHEDULED_ORDER", request.ScheduledOrderId));

        if (!request.IsAdmin)
        {
            var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
            if (restaurant is null || restaurant.RestaurantId != scheduledOrder.RestaurantId)
                return Result<ScheduledOrderDto>.Failure(
                    Error.Unauthorized("FORBIDDEN", "This recurring schedule is not accessible."));
        }

        var recurrenceType = scheduledOrder.RecurrenceType;
        if (request.RecurrenceType is not null
            && !ScheduledOrderParsing.TryParseRecurrenceType(request.RecurrenceType, out recurrenceType))
        {
            return Result<ScheduledOrderDto>.Failure(Error.Validation(
                "VALIDATION_ERROR", "RecurrenceType must be 'daily' or 'weekly'."));
        }

        var firstRunAt = request.FirstRunAt ?? scheduledOrder.FirstRunAt;
        if (request.FirstRunAt.HasValue && firstRunAt <= DateTime.UtcNow)
            return Result<ScheduledOrderDto>.Failure(Error.Validation(
                "SCHEDULED_ORDER_FIRST_RUN_IN_PAST", "FirstRunAt must be in the future."));

        if (request.DeliveryAddressId.HasValue)
        {
            var deliveryAddress = await restaurantReader.FindDeliveryAddressAsync(
                request.DeliveryAddressId.Value, scheduledOrder.RestaurantId, cancellationToken);
            if (deliveryAddress is null)
                return Result<ScheduledOrderDto>.Failure(
                    Error.NotFound("DELIVERY_ADDRESS", request.DeliveryAddressId.Value));
        }

        var updateResult = scheduledOrder.UpdateSchedule(
            recurrenceType,
            firstRunAt,
            request.Notes ?? scheduledOrder.Notes,
            request.DeliveryAddressId);

        if (updateResult.IsFailure)
            return Result<ScheduledOrderDto>.Failure(updateResult.Error);

        if (request.Items is not null)
        {
            Guid? marketId = null;
            if (marketProductReader is not null)
            {
                foreach (var marketProductId in request.Items.Select(item => item.MarketProductId).Distinct())
                {
                    var snapshot = await marketProductReader.FindAsync(marketProductId, cancellationToken);
                    if (snapshot is null)
                        return Result<ScheduledOrderDto>.Failure(Error.Validation(
                            "INVALID_PRODUCT", $"Product '{marketProductId}' is not available."));
                    if (snapshot.MarketId.HasValue && marketId.HasValue && snapshot.MarketId != marketId)
                        return Result<ScheduledOrderDto>.Failure(Error.Validation(
                            "ORDER_MARKET_MISMATCH", "A recurring order can only contain products from one market."));
                    marketId ??= snapshot.MarketId;
                }
            }
            scheduledOrder.ReplaceItems(
                request.Items.Select(i => (i.MarketProductId, i.Quantity)), marketId);
        }

        scheduledOrderRepository.Track(scheduledOrder);
        await scheduledOrderRepository.SaveChangesAsync(cancellationToken);

        return Result<ScheduledOrderDto>.Success(ScheduledOrderDtoMapper.ToDto(scheduledOrder));
    }
}
