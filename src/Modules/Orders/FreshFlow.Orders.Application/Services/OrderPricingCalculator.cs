using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Services;

internal static class OrderPricingCalculator
{
    private const double EarthRadiusKm = 6371.0088;

    public static Result<OrderPricingQuote> Calculate(
        Order order,
        IReadOnlyDictionary<Guid, MarketProductSnapshotDto> products,
        decimal? destinationLatitude,
        decimal? destinationLongitude,
        decimal deliveryFeePerKm)
    {
        if (deliveryFeePerKm < 0m)
            return Result<OrderPricingQuote>.Failure(Error.Validation(
                "INVALID_DELIVERY_FEE", "Delivery fee per kilometer cannot be negative."));

        if (destinationLatitude is null || destinationLongitude is null)
            return MissingCoordinates();

        var taxes = new Dictionary<Guid, OrderItemTaxSnapshot>();
        var origins = new HashSet<(decimal Latitude, decimal Longitude)>();
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

            if (product.OriginLatitude is null || product.OriginLongitude is null)
                return MissingCoordinates();

            origins.Add((product.OriginLatitude.Value, product.OriginLongitude.Value));
            var tax = ResolveTax(product.VatRate);
            taxes[group.Key] = tax;
            vatAmount += group.Sum(item => decimal.Round(
                item.Subtotal * tax.Percent / 100m, 2, MidpointRounding.AwayFromZero));
        }

        var distanceKm = origins.Count == 0
            ? 0m
            : origins.Max(origin => HaversineKm(
                origin.Latitude,
                origin.Longitude,
                destinationLatitude.Value,
                destinationLongitude.Value));
        distanceKm = decimal.Round(distanceKm, 2, MidpointRounding.AwayFromZero);
        var deliveryFee = decimal.Round(
            distanceKm * deliveryFeePerKm, 2, MidpointRounding.AwayFromZero);
        var subtotal = order.Items.Sum(item => item.Subtotal);

        return Result<OrderPricingQuote>.Success(new OrderPricingQuote(
            taxes,
            subtotal,
            vatAmount,
            distanceKm,
            deliveryFee,
            subtotal + vatAmount + deliveryFee));
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

    private static decimal HaversineKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        static double Radians(decimal degrees) => (double)degrees * Math.PI / 180d;

        var firstLatitude = Radians(lat1);
        var secondLatitude = Radians(lat2);
        var latitudeDelta = secondLatitude - firstLatitude;
        var longitudeDelta = Radians(lon2 - lon1);
        var a = Math.Pow(Math.Sin(latitudeDelta / 2d), 2d)
            + Math.Cos(firstLatitude) * Math.Cos(secondLatitude)
            * Math.Pow(Math.Sin(longitudeDelta / 2d), 2d);
        a = Math.Clamp(a, 0d, 1d);
        return (decimal)(EarthRadiusKm * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a)));
    }

    private static Result<OrderPricingQuote> MissingCoordinates() =>
        Result<OrderPricingQuote>.Failure(Error.Validation(
            "DELIVERY_COORDINATES_REQUIRED",
            "Delivery address and product origin coordinates are required to calculate the delivery fee."));
}

internal sealed record OrderPricingQuote(
    IReadOnlyDictionary<Guid, OrderItemTaxSnapshot> TaxesByMarketProduct,
    decimal SubtotalAmount,
    decimal VatAmount,
    decimal DeliveryDistanceKm,
    decimal DeliveryFee,
    decimal TotalAmount);
