namespace FreshFlow.Procurement.Application.Abstractions;

public interface IMarketAgentReader
{
    public Task<int> CountEligibleMarketAgentsAsync(Guid marketId, CancellationToken ct);

    public Task<IReadOnlyList<MarketAgentOptionDto>> ListEligibleMarketAgentsAsync(
        Guid marketId, CancellationToken ct);

    public Task<bool> IsEligibleMarketAgentAsync(
        Guid agentUserId,
        Guid marketId,
        CancellationToken ct);

    public Task<bool> IsAssignedToSessionAsync(
        Guid agentUserId,
        Guid marketSessionId,
        CancellationToken ct);
}

public sealed record MarketAgentOptionDto(Guid UserId, string Email, string? FullName);
