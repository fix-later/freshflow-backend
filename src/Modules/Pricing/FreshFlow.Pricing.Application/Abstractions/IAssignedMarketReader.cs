using FreshFlow.Pricing.Application.Dtos;

namespace FreshFlow.Pricing.Application.Abstractions;

/// <summary>
/// Cross-module read service: returns markets assigned to a given agent.
/// Implemented in Infrastructure using EF cross-module projections (no direct project reference).
/// </summary>
public interface IAssignedMarketReader
{
    public Task<IReadOnlyList<AssignedMarketDto>> GetByAgentIdAsync(
        Guid agentUserId, CancellationToken ct);

    /// <summary>
    /// Returns true if <paramref name="agentUserId"/> has an active assignment
    /// to <paramref name="marketId"/>.
    /// </summary>
    public Task<bool> HasAssignmentAsync(
        Guid agentUserId, Guid marketId, CancellationToken ct);
}
