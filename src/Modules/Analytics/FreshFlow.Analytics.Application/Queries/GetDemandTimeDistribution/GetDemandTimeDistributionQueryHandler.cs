using FreshFlow.Analytics.Application.Abstractions;
using FreshFlow.Analytics.Application.Common;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Analytics.Application.Queries.GetDemandTimeDistribution;

internal sealed class GetDemandTimeDistributionQueryHandler(IDemandHeatmapReader reader)
    : IRequestHandler<GetDemandTimeDistributionQuery, Result<IReadOnlyList<TimeDistributionCellDto>>>
{
    public async Task<Result<IReadOnlyList<TimeDistributionCellDto>>> Handle(
        GetDemandTimeDistributionQuery request,
        CancellationToken ct)
    {
        var (startUtc, endUtc) = VietnamTime.GetUtcBounds(request.From, request.To);
        var rows = await reader.ReadTimeDistributionAsync(startUtc, endUtc, ct);
        var cells = rows
            .Select(row => new TimeDistributionCellDto(
                row.DayOfWeek,
                row.HourOfDay,
                row.OrderCount))
            .ToArray();

        return Result<IReadOnlyList<TimeDistributionCellDto>>.Success(cells);
    }
}
