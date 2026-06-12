using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Pricing.Application.Queries.GetAssignedMarkets;

internal sealed class GetAssignedMarketsQueryHandler(IAssignedMarketReader reader)
    : IRequestHandler<GetAssignedMarketsQuery, Result<IReadOnlyList<AssignedMarketDto>>>
{
    public async Task<Result<IReadOnlyList<AssignedMarketDto>>> Handle(
        GetAssignedMarketsQuery request, CancellationToken ct)
    {
        var markets = await reader.GetByAgentIdAsync(request.AgentUserId, ct);
        return Result<IReadOnlyList<AssignedMarketDto>>.Success(markets);
    }
}
