namespace FreshFlow.Procurement.Application.Abstractions;

public interface IMarketAgentReader
{
    public Task<int> CountEligibleMarketAgentsAsync(Guid marketId, CancellationToken ct);

    public Task<bool> IsEligibleMarketAgentAsync(
        Guid agentUserId,
        Guid marketId,
        CancellationToken ct);
}
