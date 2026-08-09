using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using FreshFlow.Logistics.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Logistics.Infrastructure.Routing;

internal sealed class GoongRouteMatrixProvider(
    IHttpClientFactory clients,
    IConfiguration config,
    IVehicleCapacityPolicy settings,
    ILogger<GoongRouteMatrixProvider> logger) : IRouteMatrixProvider
{
    internal const string HttpClientName = "Logistics.Goong";
    private const double EarthRadiusMeters = 6_371_008.8;
    private const double FallbackSpeedMetersPerSecond = 30_000d / 3_600d;

    public async Task<RouteMatrixResult> GetMatrixAsync(
        IReadOnlyList<RoutePoint> points,
        IReadOnlyCollection<string> vehicleProfiles,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0)
            throw new ArgumentException("At least one point is required.", nameof(points));

        var profiles = vehicleProfiles.Distinct(StringComparer.Ordinal).Order().ToList();
        if (profiles.Count == 0)
            profiles.Add("car");
        var apiKey = config["Delivery:Goong:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith('<'))
            return Fallback(points, profiles, "Goong API key is missing; using estimated road distances.");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = new Dictionary<string, ProfileRouteMatrix>(StringComparer.Ordinal);
            foreach (var profile in profiles)
                result[profile] = await GetProfileAsync(points, profile, apiKey, ct);

            logger.LogInformation(
                "Route matrix completed Provider={Provider} Points={PointCount} Profiles={ProfileCount} ElapsedMs={ElapsedMs}",
                "GOONG", points.Count, profiles.Count, stopwatch.ElapsedMilliseconds);
            return new RouteMatrixResult(result, "GOONG", false, []);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Goong route matrix timed out; rebuilding the complete matrix with Haversine.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException)
        {
            logger.LogWarning(ex, "Goong route matrix failed; rebuilding the complete matrix with Haversine.");
        }

        return Fallback(points, profiles, "Goong matrix was unavailable; all routes use Haversine estimates.");
    }

    private async Task<ProfileRouteMatrix> GetProfileAsync(
        IReadOnlyList<RoutePoint> points, string profile, string apiKey, CancellationToken ct)
    {
        var distance = Empty(points.Count);
        var duration = Empty(points.Count);
        var batchSize = settings.MatrixBatchSize;
        for (var originStart = 0; originStart < points.Count; originStart += batchSize)
        {
            var originCount = Math.Min(batchSize, points.Count - originStart);
            for (var destinationStart = 0; destinationStart < points.Count; destinationStart += batchSize)
            {
                var destinationCount = Math.Min(batchSize, points.Count - destinationStart);
                var origins = points.Skip(originStart).Take(originCount).Select(Coordinate);
                var destinations = points.Skip(destinationStart).Take(destinationCount).Select(Coordinate);
                var url = "DistanceMatrix?origins=" + Escape(string.Join('|', origins))
                    + "&destinations=" + Escape(string.Join('|', destinations))
                    + "&vehicle=" + Escape(profile) + "&api_key=" + Escape(apiKey);

                using var response = await clients.CreateClient(HttpClientName).GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"Goong returned HTTP {(int)response.StatusCode}.");
                using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct)
                    ?? throw new InvalidDataException("Goong returned an empty matrix response.");
                ReadBatch(json.RootElement, distance, duration,
                    originStart, originCount, destinationStart, destinationCount);
            }
        }
        return new ProfileRouteMatrix(distance, duration);
    }

    private static void ReadBatch(
        JsonElement root, long[][] distances, long[][] durations,
        int originStart, int originCount, int destinationStart, int destinationCount)
    {
        if (!root.TryGetProperty("rows", out var rows)
            || rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() != originCount)
            throw new InvalidDataException("Goong returned an invalid row count.");

        for (var i = 0; i < originCount; i++)
        {
            if (!rows[i].TryGetProperty("elements", out var elements)
                || elements.ValueKind != JsonValueKind.Array
                || elements.GetArrayLength() != destinationCount)
                throw new InvalidDataException("Goong returned an invalid element count.");
            for (var j = 0; j < destinationCount; j++)
            {
                var element = elements[j];
                if (!element.TryGetProperty("status", out var status) || status.GetString() != "OK"
                    || !TryMetric(element, "distance", out var distance)
                    || !TryMetric(element, "duration", out var duration))
                    throw new InvalidDataException("Goong returned an unusable matrix element.");
                distances[originStart + i][destinationStart + j] = distance;
                durations[originStart + i][destinationStart + j] = duration;
            }
        }
    }

    private RouteMatrixResult Fallback(
        IReadOnlyList<RoutePoint> points, IReadOnlyList<string> profiles, string warning)
    {
        var factor = double.TryParse(config["Delivery:Goong:FallbackRoadFactor"],
            NumberStyles.Float, CultureInfo.InvariantCulture, out var configured) ? configured : 1.4d;
        var distance = Empty(points.Count);
        var duration = Empty(points.Count);
        for (var i = 0; i < points.Count; i++)
            for (var j = 0; j < points.Count; j++)
            {
                var meters = i == j ? 0L : checked((long)Math.Round(
                    HaversineMeters(points[i], points[j]) * factor, MidpointRounding.AwayFromZero));
                distance[i][j] = meters;
                duration[i][j] = checked((long)Math.Ceiling(meters / FallbackSpeedMetersPerSecond));
            }

        var matrices = profiles.ToDictionary(
            profile => profile,
            _ => new ProfileRouteMatrix(distance.Select(x => x.ToArray()).ToArray(),
                duration.Select(x => x.ToArray()).ToArray()),
            StringComparer.Ordinal);
        return new RouteMatrixResult(matrices, "HAVERSINE_FALLBACK", true, [warning]);
    }

    private static long[][] Empty(int count) => Enumerable.Range(0, count).Select(_ => new long[count]).ToArray();

    private static bool TryMetric(JsonElement element, string name, out long value)
    {
        value = 0;
        return element.TryGetProperty(name, out var metric)
            && metric.TryGetProperty("value", out var raw)
            && raw.TryGetInt64(out value) && value >= 0;
    }

    private static double HaversineMeters(RoutePoint from, RoutePoint to)
    {
        static double Radians(decimal value) => (double)value * Math.PI / 180d;
        var fromLatitude = Radians(from.Latitude);
        var toLatitude = Radians(to.Latitude);
        var latitudeDelta = toLatitude - fromLatitude;
        var longitudeDelta = Radians(to.Longitude - from.Longitude);
        var a = Math.Pow(Math.Sin(latitudeDelta / 2d), 2d)
            + Math.Cos(fromLatitude) * Math.Cos(toLatitude)
            * Math.Pow(Math.Sin(longitudeDelta / 2d), 2d);
        return EarthRadiusMeters * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
    }

    private static string Coordinate(RoutePoint point) => string.Create(
        CultureInfo.InvariantCulture, $"{point.Latitude},{point.Longitude}");
    private static string Escape(string value) => Uri.EscapeDataString(value);
}
