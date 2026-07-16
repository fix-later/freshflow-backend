namespace FreshFlow.Analytics.Application.Abstractions;

public interface IOrderMetricsReader
{
    public Task<IReadOnlyList<OrderMetricsBucketReadModel>> ReadAsync(
        DateTime startUtcInclusive,
        DateTime endUtcExclusive,
        Guid? restaurantId,
        string groupBy,
        CancellationToken ct);
}

public sealed record OrderMetricsBucketReadModel(
    DateOnly Date,
    string Status,
    int OrderCount,
    decimal TotalAmountVND);
