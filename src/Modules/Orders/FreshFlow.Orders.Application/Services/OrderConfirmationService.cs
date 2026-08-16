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
    IOperationalSettingsRepository operationalSettings,
    IRoadDistanceProvider roadDistanceProvider,
    IMarketSessionGate? marketSessions = null) : IOrderConfirmationService
{
    public async Task<Result<RoadDistanceResult>> GetRoadDistanceAsync(
        IReadOnlyCollection<Guid> marketProductIds,
        Guid restaurantId,
        Guid deliveryAddressId,
        CancellationToken cancellationToken)
    {
        var deliveryAddress = await restaurantReader.FindDeliveryAddressAsync(
            deliveryAddressId, restaurantId, cancellationToken);
        if (deliveryAddress is null)
            return Result<RoadDistanceResult>.Failure(Error.NotFound("DELIVERY_ADDRESS", deliveryAddressId));

        var products = new List<MarketProductSnapshotDto>();
        foreach (var marketProductId in marketProductIds.Distinct())
        {
            var product = await marketProductReader.FindAsync(marketProductId, cancellationToken);
            if (product is null)
                return Result<RoadDistanceResult>.Failure(Error.NotFound("MARKET_PRODUCT", marketProductId));
            products.Add(product);
        }

        var result = await RoadDistanceCalculator.GetAsync(
            roadDistanceProvider,
            products,
            deliveryAddress.Latitude,
            deliveryAddress.Longitude,
            cancellationToken);
        return result.IsFailure
            ? result
            : Result<RoadDistanceResult>.Success(result.Value with
            {
                InputRevision = CreateInputRevision(products, deliveryAddress)
            });
    }

    public async Task<Result<OrderDto>> ConfirmAsync(
        Order order,
        Guid restaurantId,
        Guid deliveryAddressId,
        RoadDistanceResult roadDistance,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var canConfirmResult = order.CanConfirm();
        if (canConfirmResult.IsFailure)
            return Result<OrderDto>.Failure(canConfirmResult.Error);

        var deliveryAddress = await restaurantReader.FindDeliveryAddressAsync(
            deliveryAddressId, restaurantId, cancellationToken);
        if (deliveryAddress is null)
            return Result<OrderDto>.Failure(
                Error.NotFound("DELIVERY_ADDRESS", deliveryAddressId));

        var products = new Dictionary<Guid, MarketProductSnapshotDto>();
        foreach (var marketProductId in order.Items.Select(item => item.MarketProductId).Distinct())
        {
            var product = await marketProductReader.FindAsync(marketProductId, cancellationToken);
            if (product is null)
                return Result<OrderDto>.Failure(Error.NotFound("MARKET_PRODUCT", marketProductId));
            products[marketProductId] = product;
        }

        var marketIds = products.Values
            .Where(product => product.MarketId.HasValue)
            .Select(product => product.MarketId!.Value)
            .Distinct()
            .ToArray();
        if (marketIds.Length > 1)
            return Result<OrderDto>.Failure(Error.Validation(
                "ORDER_MARKET_MISMATCH", "An order can only contain products from one market."));
        if (marketIds.Length == 1)
        {
            var assignMarket = order.AssignMarket(marketIds[0]);
            if (assignMarket.IsFailure)
                return Result<OrderDto>.Failure(assignMarket.Error);
        }

        if (roadDistance.InputRevision != CreateInputRevision(products.Values, deliveryAddress))
            return Result<OrderDto>.Failure(Error.Conflict(
                "ROUTING_INPUTS_CHANGED",
                "The order items, product origins, or delivery address changed. Retry confirmation."));

        var settings = await operationalSettings.GetAsync(cancellationToken);

        var pricing = OrderPricingCalculator.Calculate(
            order,
            products,
            roadDistance.DistanceMeters / 1000m,
            new DeliveryFeePolicy(
                settings.BaseFee,
                settings.DeliveryFeePerKm,
                settings.MinimumFee,
                settings.RoundingUnit));
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

        Guid? marketSessionId = null;
        if (marketSessions is not null && evaluation.ResolvedScheduledFor.HasValue)
        {
            var gate = await marketSessions.CheckAsync(
                order.MarketId,
                OrderCutoffScheduler.GetServiceDate(evaluation.ResolvedScheduledFor.Value),
                true,
                cancellationToken);
            if (!gate.Exists)
                return Result<OrderDto>.Failure(Error.Validation(
                    "MARKET_SESSION_NOT_AVAILABLE", "No market session is available for the delivery date."));
            if (!gate.IsOpen)
                return Result<OrderDto>.Failure(Error.Conflict(
                    "MARKET_SESSION_NOT_OPEN", "The market session is no longer accepting orders."));
            // Hard capacity ceiling: reject a confirm that would push the session's goods weight
            // past the admin-configured PlannedCapacityKg. Correctness under concurrent confirms
            // relies on the surrounding SERIALIZABLE transaction (same guarantee as stock reservation).
            if (gate.PlannedCapacityKg is { } capacityKg && capacityKg > 0m
                && gate.ConfirmedGoodsKg + order.Items.Sum(item => item.Quantity) > capacityKg)
                return Result<OrderDto>.Failure(Error.Conflict(
                    "MARKET_SESSION_CAPACITY_EXCEEDED", "The market session has reached its planned capacity."));
            marketSessionId = gate.SessionId;
        }

        var pricingResult = order.ApplyConfirmationPricing(
            pricing.Value.TaxesByMarketProduct,
            pricing.Value.DeliveryDistanceKm,
            pricing.Value.DeliveryFee,
            roadDistance.DistanceMeters,
            roadDistance.DurationSeconds,
            nowUtc,
            roadDistance.Provider,
            roadDistance.ChosenOrigin.Latitude,
            roadDistance.ChosenOrigin.Longitude);
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

        var confirmResult = order.Confirm(marketSessionId, nowUtc);
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

    private static string CreateInputRevision(
        IEnumerable<MarketProductSnapshotDto> products,
        DeliveryAddressSourceDto deliveryAddress)
    {
        var origins = string.Join('|', products
            .OrderBy(product => product.MarketProductId)
            .Select(product => FormattableString.Invariant(
                $"{product.MarketProductId:N}:{product.OriginLatitude}:{product.OriginLongitude}")));
        return FormattableString.Invariant(
            $"{deliveryAddress.AddressId:N}:{deliveryAddress.Latitude}:{deliveryAddress.Longitude}|{origins}");
    }
}
