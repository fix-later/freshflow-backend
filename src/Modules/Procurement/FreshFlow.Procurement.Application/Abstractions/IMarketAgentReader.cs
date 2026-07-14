namespace FreshFlow.Procurement.Application.Abstractions;

public interface IMarketAgentReader
{
    public Task<bool> IsEligibleMarketAgentAsync(
        Guid agentUserId,
        Guid marketId,
        CancellationToken ct);
}
