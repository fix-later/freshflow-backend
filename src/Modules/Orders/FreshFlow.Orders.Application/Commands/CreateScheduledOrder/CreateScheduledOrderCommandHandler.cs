using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Services;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Commands.CreateScheduledOrder;

internal sealed class CreateScheduledOrderCommandHandler(
    IScheduledOrderRepository scheduledOrderRepository,
    IRestaurantReader restaurantReader,
    IMarketProductReader? marketProductReader = null)
    : IRequestHandler<CreateScheduledOrderCommand, Result<ScheduledOrderDto>>
{
    public async Task<Result<ScheduledOrderDto>> Handle(
        CreateScheduledOrderCommand request, CancellationToken cancellationToken)
    {
        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null)
            return Result<ScheduledOrderDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "The authenticated user has no associated restaurant."));

        if (!restaurant.IsApproved)
            return Result<ScheduledOrderDto>.Failure(Error.Validation(
                "RESTAURANT_NOT_APPROVED", "This restaurant has not been approved to create recurring orders."));

        if (!ScheduledOrderParsing.TryParseRecurrenceType(request.RecurrenceType, out var recurrenceType))
            return Result<ScheduledOrderDto>.Failure(Error.Validation(
                "VALIDATION_ERROR", "RecurrenceType must be 'daily' or 'weekly'."));

        if (request.FirstRunAt <= DateTime.UtcNow)
            return Result<ScheduledOrderDto>.Failure(Error.Validation(
                "SCHEDULED_ORDER_FIRST_RUN_IN_PAST", "FirstRunAt must be in the future."));

        // Fail fast — validating on every job run instead would repeat the same error forever.
        var deliveryAddress = await restaurantReader.FindDeliveryAddressAsync(
            request.DeliveryAddressId, restaurant.RestaurantId, cancellationToken);
        if (deliveryAddress is null)
            return Result<ScheduledOrderDto>.Failure(
                Error.NotFound("DELIVERY_ADDRESS", request.DeliveryAddressId));

        var scheduledOrder = new ScheduledOrder(
            restaurant.RestaurantId,
            recurrenceType,
            request.FirstRunAt,
            request.Notes,
            request.DeliveryAddressId);

        Guid? marketId = null;
        if (marketProductReader is not null)
        {
            foreach (var group in request.Items.GroupBy(item => item.MarketProductId))
            {
                var snapshot = await marketProductReader.FindAsync(group.Key, cancellationToken);
                if (snapshot is null)
                    return Result<ScheduledOrderDto>.Failure(Error.Validation(
                        "INVALID_PRODUCT", $"Product '{group.Key}' is not available."));
                var quantityResult = OrderPricingCalculator.ValidateQuantity(
                    group.Sum(item => item.Quantity), snapshot);
                if (quantityResult.IsFailure)
                    return Result<ScheduledOrderDto>.Failure(quantityResult.Error);
                if (snapshot.MarketId.HasValue && marketId.HasValue && snapshot.MarketId != marketId)
                    return Result<ScheduledOrderDto>.Failure(Error.Validation(
                        "ORDER_MARKET_MISMATCH", "A recurring order can only contain products from one market."));
                marketId ??= snapshot.MarketId;
            }
        }
        if (marketId.HasValue)
            scheduledOrder.AssignMarket(marketId.Value);
        foreach (var item in request.Items)
            scheduledOrder.AddItem(item.MarketProductId, item.Quantity);

        await scheduledOrderRepository.AddAsync(scheduledOrder, cancellationToken);
        await scheduledOrderRepository.SaveChangesAsync(cancellationToken);

        return Result<ScheduledOrderDto>.Success(ScheduledOrderDtoMapper.ToDto(scheduledOrder));
    }
}
