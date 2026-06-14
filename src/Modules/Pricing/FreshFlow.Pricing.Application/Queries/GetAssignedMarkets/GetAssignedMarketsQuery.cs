using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Pricing.Application.Queries.GetAssignedMarkets;

public sealed record GetAssignedMarketsQuery(Guid AgentUserId)
    : IQuery<IReadOnlyList<AssignedMarketDto>>;
