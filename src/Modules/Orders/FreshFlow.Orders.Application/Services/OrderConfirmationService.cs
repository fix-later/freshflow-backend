using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Services;

/// <inheritdoc cref="IOrderConfirmationService"/>
public sealed class OrderConfirmationService(
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader,
    IMarketProductReader marketProductReader,
    ICreditService creditService,
    IOperationalSettingsRepository operationalSettings) : IOrderConfirmationService
{
    public async Task<Result<OrderDto>> ConfirmAsync(
        Order order, Guid restaurantId, Guid deliveryAddressId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var canConfirmResult = order.CanConfirm();
        if (canConfirmResult.IsFailure)
            return Result<OrderDto>.Failure(canConfirmResult.Error);

        var deliveryAddress = await restaurantReader.FindDeliveryAddressAsync(
            deliveryAddressId, restaurantId, cancellationToken);
        if (deliveryAddress is null)
            return Result<OrderDto>.Failure(
                Error.NotFound("DELIVERY_ADDRESS", deliveryAddressId));

        var settings = await operationalSettings.GetAsync(cancellationToken);
        var products = new Dictionary<Guid, MarketProductSnapshotDto>();
        foreach (var marketProductId in order.Items.Select(item => item.MarketProductId).Distinct())
        {
            var product = await marketProductReader.FindAsync(marketProductId, cancellationToken);
            if (product is null)
                return Result<OrderDto>.Failure(Error.NotFound("MARKET_PRODUCT", marketProductId));
            products[marketProductId] = product;
        }

        var pricing = OrderPricingCalculator.Calculate(
            order,
            products,
            deliveryAddress.Latitude,
            deliveryAddress.Longitude,
            settings.DeliveryFeePerKm);
        if (pricing.IsFailure)
            return Result<OrderDto>.Failure(pricing.Error);

        var canChargeResult = await creditService.CanChargeAsync(
            restaurantId, pricing.Value.TotalAmount, cancellationToken);
        if (canChargeResult.IsFailure)
            return Result<OrderDto>.Failure(canChargeResult.Error);

        var evaluation = OrderConfirmationEvaluator.Evaluate(
            order,
            canChargeResult.Value,
            nowUtc,
            settings.DeliveryWindowDays,
            settings.DailyCutoffTime.ToTimeSpan(),
            pricing.Value.TotalAmount);
        if (evaluation.Issues.Count > 0)
            return Result<OrderDto>.Failure(evaluation.Issues[0]);

        var pricingResult = order.ApplyConfirmationPricing(
            pricing.Value.TaxesByMarketProduct,
            pricing.Value.DeliveryDistanceKm,
            pricing.Value.DeliveryFee);
        if (pricingResult.IsFailure)
            return Result<OrderDto>.Failure(pricingResult.Error);

        var reservations = order.Items
            .GroupBy(item => item.MarketProductId)
            .Select(group => new StockReservation(group.Key, group.Sum(item => item.Quantity)))
            .OrderBy(reservation => reservation.MarketProductId)
            .ToArray();

        if (!await orderRepository.TryReserveStockAsync(reservations, cancellationToken))
            return Result<OrderDto>.Failure(Error.Validation(
                "INSUFFICIENT_STOCK",
                "One or more products no longer have enough available stock."));

        var addressResult = order.CaptureDeliveryAddress(
            deliveryAddress.AddressId,
            deliveryAddress.RecipientName,
            deliveryAddress.Phone,
            deliveryAddress.AddressLine,
            deliveryAddress.Latitude,
            deliveryAddress.Longitude);
        if (addressResult.IsFailure)
            return Result<OrderDto>.Failure(addressResult.Error);

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
            restaurantId, order.Id, order.TotalAmount, "Order confirmed", cancellationToken);
        if (chargeResult.IsFailure)
            return Result<OrderDto>.Failure(chargeResult.Error);

        // Seam for SCRUM-266 (credit-limit alert): chargeResult.Value already exposes the
        // post-charge CreditLimit/OutstandingBalance/AvailableCredit for this restaurant, so a
        // future threshold check (and its integration event to Notifications) can hook in right
        // here without touching CreditService or the order-confirmation flow above.

        return Result<OrderDto>.Success(OrderDtoMapper.ToDto(order));
    }
}
