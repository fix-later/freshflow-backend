using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Analytics.Application.Queries.GetPriceTrends;

internal sealed class GetPriceTrendsQueryHandler(IPriceTrendReader reader)
    : IRequestHandler<GetPriceTrendsQuery, Result<PriceTrendsDto>>
{
    private const string DailyInterval = "daily";
    private const string HourlyInterval = "hourly";
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();
    private static readonly DateOnly LatestDateWith12MonthsRemaining =
        DateOnly.MaxValue.AddMonths(-12);

    public async Task<Result<PriceTrendsDto>> Handle(
        GetPriceTrendsQuery request,
        CancellationToken ct)
    {
        var spansMoreThanTwelveMonths =
            request.From <= LatestDateWith12MonthsRemaining &&
            request.To > request.From.AddMonths(12);
        var interval = spansMoreThanTwelveMonths
            ? DailyInterval
            : request.Interval?.ToLowerInvariant() ?? DailyInterval;
        var localStart = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEnd = request.To == DateOnly.MaxValue
            ? DateTime.MaxValue
            : request.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, VietnamTimeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, VietnamTimeZone);
        var requestedIds = request.MarketProductIds.Distinct().ToArray();
        var data = await reader.ReadAsync(requestedIds, startUtc, endUtc, ct);
        var detailsById = data.MarketProducts.ToDictionary(detail => detail.MarketProductId);
        var bucketsById = data.HourlyBuckets.ToLookup(bucket => bucket.MarketProductId);
        var series = new List<PriceTrendSeriesDto>(requestedIds.Length);

        foreach (var marketProductId in requestedIds)
        {
            var hourlyBuckets = bucketsById[marketProductId]
                .OrderBy(bucket => bucket.BucketStartUtc)
                .ToArray();
            if (hourlyBuckets.Length == 0 ||
                !detailsById.TryGetValue(marketProductId, out var detail))
            {
                continue;
            }

            var summary = Aggregate(hourlyBuckets);
            var points = hourlyBuckets
                .GroupBy(bucket => ToLocalBucket(bucket.BucketStartUtc, interval))
                .OrderBy(group => group.Key)
                .Select(group =>
                {
                    var aggregate = Aggregate(group);
                    return new PriceTrendPointDto(
                        new DateTimeOffset(group.Key, VietnamTimeZone.GetUtcOffset(group.Key)),
                        aggregate.AvgPrice,
                        aggregate.MinPrice,
                        aggregate.MaxPrice,
                        aggregate.SnapshotCount);
                })
                .ToArray();

            series.Add(new PriceTrendSeriesDto(
                marketProductId,
                detail.ProductName,
                detail.MarketName,
                interval,
                new PriceTrendSummaryDto(
                    summary.MinPrice,
                    summary.MaxPrice,
                    summary.AvgPrice,
                    summary.PriceVolatility),
                points));
        }

        return Result<PriceTrendsDto>.Success(new PriceTrendsDto(series));
    }

    private static DateTime ToLocalBucket(DateTime bucketStartUtc, string interval)
    {
        var utc = DateTime.SpecifyKind(bucketStartUtc, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamTimeZone);
        return interval == HourlyInterval
            ? new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0, DateTimeKind.Unspecified)
            : new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified);
    }

    private static PriceAggregate Aggregate(IEnumerable<PriceTrendHourlyBucketReadModel> source)
    {
        var buckets = source.ToArray();
        var snapshotCount = buckets.Sum(bucket => bucket.SnapshotCount);
        var avgPrice = buckets.Sum(bucket => bucket.AvgPrice * bucket.SnapshotCount) / snapshotCount;
        decimal? volatility = null;

        if (snapshotCount >= 2)
        {
            var squaredDeviations = buckets.Sum(bucket =>
            {
                var withinBucket = bucket.PriceVolatility is { } value
                    ? (bucket.SnapshotCount - 1) * value * value
                    : 0m;
                var distanceFromMean = bucket.AvgPrice - avgPrice;
                return withinBucket + bucket.SnapshotCount * distanceFromMean * distanceFromMean;
            });
            volatility = (decimal)Math.Sqrt((double)(squaredDeviations / (snapshotCount - 1)));
        }

        return new PriceAggregate(
            buckets.Min(bucket => bucket.MinPrice),
            buckets.Max(bucket => bucket.MaxPrice),
            avgPrice,
            snapshotCount,
            volatility);
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }

    private sealed record PriceAggregate(
        decimal MinPrice,
        decimal MaxPrice,
        decimal AvgPrice,
        int SnapshotCount,
        decimal? PriceVolatility);
}
