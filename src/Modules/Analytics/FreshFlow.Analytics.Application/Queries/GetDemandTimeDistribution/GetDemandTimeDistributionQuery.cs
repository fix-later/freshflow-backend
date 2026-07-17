using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Analytics.Application.Queries.GetDemandTimeDistribution;

public sealed record GetDemandTimeDistributionQuery(
    DateOnly From,
    DateOnly To) : IQuery<IReadOnlyList<TimeDistributionCellDto>>;
