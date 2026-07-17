using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Analytics.Application.Queries.GetProcurementMetrics;

public sealed record GetProcurementMetricsQuery(
    DateOnly From,
    DateOnly To,
    Guid? MarketId) : IQuery<ProcurementMetricsDto>;
