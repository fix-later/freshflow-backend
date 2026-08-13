using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Procurement.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketAgentReader(AppDbContext db) : IMarketAgentReader
{
    public Task<int> CountEligibleMarketAgentsAsync(Guid marketId, CancellationToken ct) =>
        EligibleAssignments(marketId).Select(assignment => assignment.UserId).Distinct().CountAsync(ct);

    public Task<bool> IsEligibleMarketAgentAsync(
        Guid agentUserId,
        Guid marketId,
        CancellationToken ct) =>
        EligibleAssignments(marketId)
            .Where(assignment => assignment.UserId == agentUserId)
            .AnyAsync(ct);

    private IQueryable<UserMarketAssignmentRow> EligibleAssignments(Guid marketId) =>
        db.Set<UserMarketAssignmentRow>()
            .Where(assignment => assignment.MarketId == marketId)
            .Join(
                db.Set<MarketAgentUserRow>().Where(user =>
                    user.IsActive &&
                    user.DeletedAt == null &&
                    user.RoleName == "market_agent"),
                assignment => assignment.UserId,
                user => user.Id,
                (assignment, _) => assignment);
}
