using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketAgentReader(AppDbContext db) : IMarketAgentReader
{
    public Task<int> CountEligibleMarketAgentsAsync(Guid marketId, CancellationToken ct) =>
        EligibleAgents(marketId).Select(user => user.Id).Distinct().CountAsync(ct);

    public async Task<IReadOnlyList<MarketAgentOptionDto>> ListEligibleMarketAgentsAsync(
        Guid marketId, CancellationToken ct) =>
        await EligibleAgents(marketId)
            .OrderBy(user => user.FullName ?? user.Email)
            .Select(user => new MarketAgentOptionDto(user.Id, user.Email, user.FullName))
            .ToListAsync(ct);

    public Task<bool> IsEligibleMarketAgentAsync(
        Guid agentUserId,
        Guid marketId,
        CancellationToken ct) =>
        EligibleAgents(marketId)
            .Where(user => user.Id == agentUserId)
            .AnyAsync(ct);

    public Task<bool> IsAssignedToSessionAsync(
        Guid agentUserId,
        Guid marketSessionId,
        CancellationToken ct) =>
        db.Set<MarketSessionAgent>()
            .AnyAsync(row => row.SessionId == marketSessionId && row.UserId == agentUserId, ct);

    private IQueryable<MarketAgentUserRow> EligibleAgents(Guid marketId) =>
        db.Set<UserMarketAssignmentRow>()
            .Where(assignment => assignment.MarketId == marketId)
            .Join(
                db.Set<MarketAgentUserRow>().Where(user =>
                    user.IsActive &&
                    user.DeletedAt == null &&
                    user.RoleName == "market_agent"),
                assignment => assignment.UserId,
                user => user.Id,
                (_, user) => user);
}
