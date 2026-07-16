using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Analytics.Application.Queries.GetHubThroughput;

public sealed record GetHubThroughputQuery(
    DateOnly From,
    DateOnly To,
    Guid? HubId) : IQuery<HubThroughputDto>;
