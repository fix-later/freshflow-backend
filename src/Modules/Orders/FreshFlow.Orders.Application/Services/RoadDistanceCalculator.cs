using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Services;

internal static class RoadDistanceCalculator
{
    public static async Task<Result<RoadDistanceResult>> GetAsync(
        IRoadDistanceProvider provider,
        IEnumerable<MarketProductSnapshotDto> products,
        decimal? destinationLatitude,
        decimal? destinationLongitude,
        CancellationToken cancellationToken)
    {
        if (destinationLatitude is null || destinationLongitude is null)
            return MissingCoordinates();

        var origins = new HashSet<GeoCoordinate>();
        foreach (var product in products)
        {
            if (product.OriginLatitude is null || product.OriginLongitude is null)
                return MissingCoordinates();
            origins.Add(new GeoCoordinate(product.OriginLatitude.Value, product.OriginLongitude.Value));
        }

        var destination = new GeoCoordinate(destinationLatitude.Value, destinationLongitude.Value);
        if (origins.Count == 0)
            return Result<RoadDistanceResult>.Success(
                new RoadDistanceResult(0, 0, destination, false, "NONE"));

        try
        {
            return Result<RoadDistanceResult>.Success(
                await provider.GetDistanceAsync(origins.ToArray(), destination, cancellationToken));
        }
        catch (ArgumentException)
        {
            return MissingCoordinates();
        }
    }

    private static Result<RoadDistanceResult> MissingCoordinates() =>
        Result<RoadDistanceResult>.Failure(Error.Validation(
            "DELIVERY_COORDINATES_REQUIRED",
            "Delivery address and product origin coordinates are required to calculate the delivery fee."));
}
