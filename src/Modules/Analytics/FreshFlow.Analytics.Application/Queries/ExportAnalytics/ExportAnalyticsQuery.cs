using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Analytics.Application.Queries.ExportAnalytics;

public sealed record ExportAnalyticsQuery(
    string Dataset,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<Guid> MarketProductIds,
    string? Format) : IQuery<CsvExportDto>;
