namespace FreshFlow.Analytics.Application.Abstractions;

public interface IHubThroughputReader
{
    public Task<HubThroughputReadModel> ReadAsync(
        DateTime startUtcInclusive,
        DateTime endUtcExclusive,
        Guid? hubId,
        CancellationToken ct);
}

public sealed record HubThroughputReadModel(
    IReadOnlyList<HubThroughputBucketReadModel> InboundBuckets,
    IReadOnlyList<HubThroughputBucketReadModel> OutboundBuckets,
    IReadOnlyList<HubThroughputStatusCountReadModel> InboundStatusCounts);

public sealed record HubThroughputBucketReadModel(
    DateOnly Date,
    Guid HubId,
    string HubName,
    decimal Kg,
    int EventCount);

public sealed record HubThroughputStatusCountReadModel(string Status, int Count);
