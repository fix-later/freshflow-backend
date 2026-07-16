using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Common;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Analytics.Application.Queries.GetHubThroughput;

internal sealed class GetHubThroughputQueryHandler(IHubThroughputReader reader)
    : IRequestHandler<GetHubThroughputQuery, Result<HubThroughputDto>>
{
    private static readonly string[] InboundStatuses =
    [
        "PENDING",
        "ARRIVED_AT_HUB"
    ];

    public async Task<Result<HubThroughputDto>> Handle(
        GetHubThroughputQuery request,
        CancellationToken ct)
    {
        var (startUtc, endUtc) = VietnamTime.GetUtcBounds(request.From, request.To);
        var metrics = await reader.ReadAsync(startUtc, endUtc, request.HubId, ct);
        var observedStatusCounts = metrics.InboundStatusCounts.ToDictionary(
            row => row.Status,
            row => row.Count);
        var statusCounts = InboundStatuses.ToDictionary(
            status => status,
            status => observedStatusCounts.GetValueOrDefault(status));
        var buckets = metrics.InboundBuckets
            .Select(row => new HubThroughputBucketDto(
                row.Date,
                row.HubId,
                row.HubName,
                row.Kg,
                0m,
                row.EventCount,
                0))
            .Concat(metrics.OutboundBuckets.Select(row => new HubThroughputBucketDto(
                row.Date,
                row.HubId,
                row.HubName,
                0m,
                row.Kg,
                0,
                row.EventCount)))
            .GroupBy(row => new { row.Date, row.HubId, row.HubName })
            .OrderBy(group => group.Key.Date)
            .ThenBy(group => group.Key.HubName)
            .Select(group => new HubThroughputBucketDto(
                group.Key.Date,
                group.Key.HubId,
                group.Key.HubName,
                group.Sum(row => row.InboundKg),
                group.Sum(row => row.OutboundKg),
                group.Sum(row => row.InboundEventCount),
                group.Sum(row => row.OutboundEventCount)))
            .ToArray();
        var inboundKg = metrics.InboundBuckets.Sum(row => row.Kg);
        var outboundKg = metrics.OutboundBuckets.Sum(row => row.Kg);

        return Result<HubThroughputDto>.Success(new HubThroughputDto(
            new HubThroughputSummaryDto(
                inboundKg,
                outboundKg,
                inboundKg - outboundKg,
                metrics.InboundBuckets.Sum(row => row.EventCount),
                metrics.OutboundBuckets.Sum(row => row.EventCount),
                statusCounts),
            buckets));
    }
}
