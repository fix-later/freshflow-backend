using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using FreshFlow.Orders.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FreshFlow.Orders.Infrastructure.Goong;

internal sealed class GoongRoadDistanceProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<GoongOptions> options,
    ILogger<GoongRoadDistanceProvider> logger) : IRoadDistanceProvider
{
    internal const string HttpClientName = "Orders.Goong";
    private const double EarthRadiusMeters = 6_371_008.8;
    private const double FallbackSpeedMetersPerSecond = 30_000d / 3_600d;

    private readonly GoongOptions _options = options.Value;

    public async Task<RoadDistanceResult> GetDistanceAsync(
        IReadOnlyList<GeoCoordinate> origins,
        GeoCoordinate destination,
        CancellationToken cancellationToken)
    {
        Validate(origins, destination);

        if (string.IsNullOrWhiteSpace(_options.ApiKey) || _options.ApiKey.StartsWith('<'))
            return Fallback(origins, destination);

        try
        {
            var result = origins.Count == 1
                ? await GetDirectionAsync(origins[0], destination, cancellationToken)
                : await GetMatrixAsync(origins, destination, cancellationToken);

            if (result is not null)
                return result;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Goong routing timed out; using Haversine fallback.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            logger.LogWarning(exception, "Goong routing failed; using Haversine fallback.");
        }

        return Fallback(origins, destination);
    }

    private async Task<RoadDistanceResult?> GetDirectionAsync(
        GeoCoordinate origin,
        GeoCoordinate destination,
        CancellationToken cancellationToken)
    {
        using var response = await httpClientFactory.CreateClient(HttpClientName).GetAsync(
            $"Direction?origin={Coordinate(origin)}&destination={Coordinate(destination)}" +
            $"&vehicle={Escape(_options.Vehicle)}&api_key={Escape(_options.ApiKey)}",
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        if (json is null
            || !TryMetric(json.RootElement, ["routes", "0", "legs", "0"], out var distance, out var duration))
            return null;

        return new RoadDistanceResult(distance, duration, origin, false, "GOONG");
    }

    private async Task<RoadDistanceResult?> GetMatrixAsync(
        IReadOnlyList<GeoCoordinate> origins,
        GeoCoordinate destination,
        CancellationToken cancellationToken)
    {
        var encodedOrigins = Escape(string.Join('|', origins.Select(Coordinate)));
        using var response = await httpClientFactory.CreateClient(HttpClientName).GetAsync(
            $"DistanceMatrix?origins={encodedOrigins}&destinations={Coordinate(destination)}" +
            $"&vehicle={Escape(_options.Vehicle)}&api_key={Escape(_options.ApiKey)}",
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        if (json is null
            || !json.RootElement.TryGetProperty("rows", out var rows)
            || rows.ValueKind != JsonValueKind.Array
            || rows.GetArrayLength() != origins.Count)
            return null;

        RoadDistanceResult? farthest = null;
        for (var i = 0; i < origins.Count; i++)
        {
            if (rows[i].ValueKind != JsonValueKind.Object
                || !rows[i].TryGetProperty("elements", out var elements)
                || elements.ValueKind != JsonValueKind.Array
                || elements.GetArrayLength() == 0
                || elements[0].ValueKind != JsonValueKind.Object
                || !elements[0].TryGetProperty("status", out var status)
                || status.GetString() != "OK"
                || !TryMetric(elements[0], [], out var distance, out var duration))
                return null;

            if (farthest is null || distance > farthest.DistanceMeters)
                farthest = new RoadDistanceResult(distance, duration, origins[i], false, "GOONG");
        }

        return farthest;
    }

    private RoadDistanceResult Fallback(IReadOnlyList<GeoCoordinate> origins, GeoCoordinate destination)
    {
        var chosen = origins
            .Select(origin => (Origin: origin, Meters: HaversineMeters(origin, destination)))
            .MaxBy(candidate => candidate.Meters);
        var distance = checked((int)Math.Round(
            chosen.Meters * _options.FallbackRoadFactor,
            MidpointRounding.AwayFromZero));
        var duration = checked((int)Math.Ceiling(distance / FallbackSpeedMetersPerSecond));
        return new RoadDistanceResult(distance, duration, chosen.Origin, true, "HAVERSINE_FALLBACK");
    }

    private static bool TryMetric(
        JsonElement root,
        IReadOnlyList<string> path,
        out int distance,
        out int duration)
    {
        distance = duration = 0;
        foreach (var segment in path)
        {
            if (int.TryParse(segment, out var index))
            {
                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() <= index)
                {
                    distance = duration = 0;
                    return false;
                }
                root = root[index];
            }
            else if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty(segment, out root))
            {
                distance = duration = 0;
                return false;
            }
        }

        return TryValue(root, "distance", out distance)
            && TryValue(root, "duration", out duration)
            && distance >= 0
            && duration >= 0;
    }

    private static bool TryValue(JsonElement root, string property, out int value)
    {
        value = 0;
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(property, out var metric)
            && metric.ValueKind == JsonValueKind.Object
            && metric.TryGetProperty("value", out var raw)
            && raw.TryGetInt32(out value);
    }

    private static void Validate(IReadOnlyList<GeoCoordinate> origins, GeoCoordinate destination)
    {
        ArgumentNullException.ThrowIfNull(origins);
        ArgumentNullException.ThrowIfNull(destination);
        if (origins.Count == 0)
            throw new ArgumentException("At least one origin is required.", nameof(origins));

        ValidateCoordinate(destination, nameof(destination));
        foreach (var origin in origins)
        {
            ArgumentNullException.ThrowIfNull(origin);
            ValidateCoordinate(origin, nameof(origins));
        }
    }

    private static void ValidateCoordinate(GeoCoordinate coordinate, string parameterName)
    {
        if (coordinate.Latitude is < -90m or > 90m
            || coordinate.Longitude is < -180m or > 180m)
            throw new ArgumentOutOfRangeException(parameterName, "Coordinates are outside valid latitude/longitude ranges.");
    }

    private static double HaversineMeters(GeoCoordinate origin, GeoCoordinate destination)
    {
        static double Radians(decimal degrees) => (double)degrees * Math.PI / 180d;

        var originLatitude = Radians(origin.Latitude);
        var destinationLatitude = Radians(destination.Latitude);
        var latitudeDelta = destinationLatitude - originLatitude;
        var longitudeDelta = Radians(destination.Longitude - origin.Longitude);
        var a = Math.Pow(Math.Sin(latitudeDelta / 2d), 2d)
            + Math.Cos(originLatitude) * Math.Cos(destinationLatitude)
            * Math.Pow(Math.Sin(longitudeDelta / 2d), 2d);
        a = Math.Clamp(a, 0d, 1d);
        return EarthRadiusMeters * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
    }

    private static string Coordinate(GeoCoordinate coordinate) => string.Create(
        CultureInfo.InvariantCulture,
        $"{coordinate.Latitude},{coordinate.Longitude}");

    private static string Escape(string value) => Uri.EscapeDataString(value);
}
