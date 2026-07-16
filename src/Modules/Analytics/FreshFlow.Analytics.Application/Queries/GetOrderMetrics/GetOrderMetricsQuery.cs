using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Analytics.Application.Queries.GetOrderMetrics;

public sealed record GetOrderMetricsQuery(
    DateOnly From,
    DateOnly To,
    Guid? RestaurantId,
    string? GroupBy = null) : IQuery<OrderMetricsDto>;
