using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Analytics.Application.Queries.GetDeliveryPerformance;

public sealed record GetDeliveryPerformanceQuery(
    DateOnly From,
    DateOnly To) : IQuery<DeliveryPerformanceDto>;
