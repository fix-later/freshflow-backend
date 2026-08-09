using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Services;

internal static class OrderPricingCalculator
{
    public static Result<OrderPricingQuote> Calculate(
        Order order,
        IReadOnlyDictionary<Guid, MarketProductSnapshotDto> products,
        decimal deliveryDistanceKm,
        DeliveryFeePolicy deliveryFee)
    {
        if (deliveryDistanceKm < 0m
            || deliveryFee.BaseFee < 0m
            || deliveryFee.RatePerKm < 0m
            || deliveryFee.MinimumFee < 0m
            || deliveryFee.RoundingUnit < 0m)
            return Result<OrderPricingQuote>.Failure(Error.Validation(
                "INVALID_DELIVERY_FEE", "Delivery distance and fee settings cannot be negative."));

        var taxes = new Dictionary<Guid, OrderItemTaxSnapshot>();
        decimal vatAmount = 0m;

        foreach (var group in order.Items.GroupBy(item => item.MarketProductId))
        {
            if (!products.TryGetValue(group.Key, out var product))
                return Result<OrderPricingQuote>.Failure(Error.NotFound("MARKET_PRODUCT", group.Key));

            var quantity = group.Sum(item => item.Quantity);
            if (quantity < product.MinimumOrderQuantity)
            {
                return Result<OrderPricingQuote>.Failure(Error.Validation(
                    "MINIMUM_ORDER_QUANTITY_NOT_MET",
                    $"{product.ProductName} requires at least {product.MinimumOrderQuantity}; requested {quantity}."));
            }

            var tax = ResolveTax(product.VatRate);
            taxes[group.Key] = tax;
            vatAmount += group.Sum(item => decimal.Round(
                item.Subtotal * tax.Percent / 100m, 2, MidpointRounding.AwayFromZero));
        }

        var distanceKm = decimal.Round(deliveryDistanceKm, 2, MidpointRounding.AwayFromZero);
        var rawFee = Math.Max(
            deliveryFee.BaseFee + distanceKm * deliveryFee.RatePerKm,
            deliveryFee.MinimumFee);
        var calculatedFee = deliveryFee.RoundingUnit == 0m
            ? decimal.Round(rawFee, 2, MidpointRounding.AwayFromZero)
            : decimal.Round(rawFee / deliveryFee.RoundingUnit, 0, MidpointRounding.AwayFromZero)
                * deliveryFee.RoundingUnit;
        var subtotal = order.Items.Sum(item => item.Subtotal);

        return Result<OrderPricingQuote>.Success(new OrderPricingQuote(
            taxes,
            subtotal,
            vatAmount,
            distanceKm,
            calculatedFee,
            subtotal + vatAmount + calculatedFee));
    }

    private static OrderItemTaxSnapshot ResolveTax(string? rate)
    {
        var code = string.IsNullOrWhiteSpace(rate) ? "KCT" : rate.Trim().ToUpperInvariant();
        var percent = code switch
        {
            "5" => 5m,
            "8" => 8m,
            "10" => 10m,
            _ => 0m
        };
        return new OrderItemTaxSnapshot(code, percent);
    }

}

internal sealed record DeliveryFeePolicy(
    decimal BaseFee,
    decimal RatePerKm,
    decimal MinimumFee,
    decimal RoundingUnit);

internal sealed record OrderPricingQuote(
    IReadOnlyDictionary<Guid, OrderItemTaxSnapshot> TaxesByMarketProduct,
    decimal SubtotalAmount,
    decimal VatAmount,
    decimal DeliveryDistanceKm,
    decimal DeliveryFee,
    decimal TotalAmount);
