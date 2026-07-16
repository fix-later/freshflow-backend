using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Analytics.Application.Queries.GetDemandHeatmap;

public sealed record GetDemandHeatmapQuery(
    DateOnly From,
    DateOnly To) : IQuery<IReadOnlyList<DemandHeatmapPointDto>>;
