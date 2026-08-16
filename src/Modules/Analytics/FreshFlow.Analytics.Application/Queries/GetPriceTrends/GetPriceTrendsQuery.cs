using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Analytics.Application.Queries.GetPriceTrends;

public sealed record GetPriceTrendsQuery(
    IReadOnlyList<Guid> MarketProductIds,
    DateOnly From,
    DateOnly To,
    string? Interval = null) : IQuery<PriceTrendsDto>;
