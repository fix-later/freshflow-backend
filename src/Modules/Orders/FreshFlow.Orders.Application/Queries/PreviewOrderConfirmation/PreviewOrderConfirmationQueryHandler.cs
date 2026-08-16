using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Application.Services;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Orders.Application.Queries.PreviewOrderConfirmation;

internal sealed class PreviewOrderConfirmationQueryHandler(
    IOrderRepository orderRepository,
    IRestaurantReader restaurantReader,
    IMarketProductReader marketProductReader,
    ICreditService creditService,
    IOperationalSettingsRepository operationalSettings,
    IRoadDistanceProvider roadDistanceProvider,
    IMarketSessionGate? marketSessions = null)
    : IRequestHandler<PreviewOrderConfirmationQuery, Result<OrderConfirmationPreviewDto>>
{
    public Task<Result<OrderConfirmationPreviewDto>> Handle(
        PreviewOrderConfirmationQuery request, CancellationToken cancellationToken) =>
        Handle(request, cancellationToken, DateTime.UtcNow);

    internal async Task<Result<OrderConfirmationPreviewDto>> Handle(
        PreviewOrderConfirmationQuery request, CancellationToken cancellationToken, DateTime nowUtc)
    {
        var order = await orderRepository.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
            return Result<OrderConfirmationPreviewDto>.Failure(Error.NotFound("ORDER", request.OrderId));

        var restaurant = await restaurantReader.FindByUserIdAsync(request.UserId, cancellationToken);
        if (restaurant is null || restaurant.RestaurantId != order.RestaurantId)
            return Result<OrderConfirmationPreviewDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This order does not belong to the authenticated restaurant."));

        var settings = await operationalSettings.GetAsync(cancellationToken);
        OrderPricingQuote? pricing = null;
        RoadDistanceResult? roadDistance = null;
        if (request.DeliveryAddressId is Guid deliveryAddressId)
        {
            var address = await restaurantReader.FindDeliveryAddressAsync(
                deliveryAddressId, order.RestaurantId, cancellationToken);
            if (address is null)
                return Result<OrderConfirmationPreviewDto>.Failure(
                    Error.NotFound("DELIVERY_ADDRESS", deliveryAddressId));

            var products = new Dictionary<Guid, MarketProductSnapshotDto>();
            foreach (var marketProductId in order.Items.Select(item => item.MarketProductId).Distinct())
            {
                var product = await marketProductReader.FindAsync(marketProductId, cancellationToken);
                if (product is null)
                    return Result<OrderConfirmationPreviewDto>.Failure(
                        Error.NotFound("MARKET_PRODUCT", marketProductId));
                products[marketProductId] = product;
            }

            var roadDistanceResult = await RoadDistanceCalculator.GetAsync(
                roadDistanceProvider,
                products.Values,
                address.Latitude,
                address.Longitude,
                cancellationToken);
            if (roadDistanceResult.IsFailure)
                return PricingIssue(order, roadDistanceResult.Error);

            roadDistance = roadDistanceResult.Value;
            var pricingResult = OrderPricingCalculator.Calculate(
                order,
                products,
                roadDistance.DistanceMeters / 1000m,
                new DeliveryFeePolicy(
                    settings.BaseFee,
                    settings.DeliveryFeePerKm,
                    settings.MinimumFee,
                    settings.RoundingUnit));
            if (pricingResult.IsFailure)
                return PricingIssue(order, pricingResult.Error);

            pricing = pricingResult.Value;
        }

        var totalAmount = pricing?.TotalAmount ?? order.TotalAmount;
        var canChargeResult = await creditService.CanChargeAsync(
            order.RestaurantId, totalAmount, cancellationToken);
        if (canChargeResult.IsFailure)
        {
            // Unlike confirm, a credit-limit failure is a displayable preview issue, not a
            // query failure — the caller still gets a 200 with WouldSucceed=false. There is no
            // CreditCheckDto on this path, so RemainingCreditAfter stays null rather than
            // triggering an extra repo read.
            return Result<OrderConfirmationPreviewDto>.Success(new OrderConfirmationPreviewDto(
                WouldSucceed: false,
                Issues: [new PreviewIssueDto(canChargeResult.Error.Code, canChargeResult.Error.Message)],
                TotalAmount: totalAmount,
                ResolvedScheduledFor: null,
                RemainingCreditAfter: null,
                SubtotalAmount: pricing?.SubtotalAmount ?? order.TotalAmount,
                VatAmount: pricing?.VatAmount ?? 0m,
                DeliveryFee: pricing?.DeliveryFee ?? 0m,
                DeliveryDistanceKm: pricing?.DeliveryDistanceKm ?? 0m,
                DeliveryDurationSeconds: roadDistance?.DurationSeconds,
                RoutingProvider: roadDistance?.Provider,
                DeliveryDistanceEstimated: roadDistance?.IsEstimated ?? false));
        }

        var evaluation = OrderConfirmationEvaluator.Evaluate(
            order,
            canChargeResult.Value,
            nowUtc,
            settings.DeliveryWindowDays,
            settings.DailyCutoffTime.ToTimeSpan(),
            totalAmount);
        var issues = evaluation.Issues
            .Select(error => new PreviewIssueDto(error.Code, error.Message))
            .ToList();
        if (marketSessions is not null && evaluation.ResolvedScheduledFor.HasValue)
        {
            var gate = await marketSessions.CheckAsync(
                order.MarketId,
                OrderCutoffScheduler.GetServiceDate(evaluation.ResolvedScheduledFor.Value),
                false,
                cancellationToken);
            if (!gate.Exists)
                issues.Add(new PreviewIssueDto(
                    "MARKET_SESSION_NOT_AVAILABLE", "No market session is available for the delivery date."));
            else if (!gate.IsOpen)
                issues.Add(new PreviewIssueDto(
                    "MARKET_SESSION_NOT_OPEN", "The market session is no longer accepting orders."));
            else if (gate.PlannedCapacityKg is { } capacityKg && capacityKg > 0m
                && gate.ConfirmedGoodsKg + order.Items.Sum(item => item.Quantity) > capacityKg)
                issues.Add(new PreviewIssueDto(
                    "MARKET_SESSION_CAPACITY_EXCEEDED", "The market session has reached its planned capacity."));
        }

        return Result<OrderConfirmationPreviewDto>.Success(new OrderConfirmationPreviewDto(
            WouldSucceed: issues.Count == 0,
            Issues: issues,
            TotalAmount: evaluation.TotalAmount,
            ResolvedScheduledFor: evaluation.ResolvedScheduledFor,
            RemainingCreditAfter: evaluation.CreditCheck.AvailableCredit - evaluation.TotalAmount,
            SubtotalAmount: pricing?.SubtotalAmount ?? order.TotalAmount,
            VatAmount: pricing?.VatAmount ?? 0m,
            DeliveryFee: pricing?.DeliveryFee ?? 0m,
            DeliveryDistanceKm: pricing?.DeliveryDistanceKm ?? 0m,
            DeliveryDurationSeconds: roadDistance?.DurationSeconds,
            RoutingProvider: roadDistance?.Provider,
            DeliveryDistanceEstimated: roadDistance?.IsEstimated ?? false));
    }

    private static Result<OrderConfirmationPreviewDto> PricingIssue(Order order, Error error) =>
        Result<OrderConfirmationPreviewDto>.Success(new OrderConfirmationPreviewDto(
            WouldSucceed: false,
            Issues: [new PreviewIssueDto(error.Code, error.Message)],
            TotalAmount: order.TotalAmount,
            ResolvedScheduledFor: null,
            RemainingCreditAfter: null,
            SubtotalAmount: order.TotalAmount));
}
