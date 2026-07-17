namespace FreshFlow.Analytics.Application.Dtos;

public sealed record HubThroughputDto(
    HubThroughputSummaryDto Summary,
    IReadOnlyList<HubThroughputBucketDto> Buckets);

public sealed record HubThroughputSummaryDto(
    decimal InboundKg,
    decimal OutboundKg,
    decimal NetKg,
    int InboundEventCount,
    int OutboundEventCount,
    IReadOnlyDictionary<string, int> InboundStatusCounts);

public sealed record HubThroughputBucketDto(
    DateOnly Date,
    Guid HubId,
    string HubName,
    decimal InboundKg,
    decimal OutboundKg,
    int InboundEventCount,
    int OutboundEventCount);
