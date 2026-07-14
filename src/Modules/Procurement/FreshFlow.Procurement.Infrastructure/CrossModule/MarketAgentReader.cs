using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Procurement.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketAgentReader(AppDbContext db) : IMarketAgentReader
{
    public Task<bool> IsEligibleMarketAgentAsync(
        Guid agentUserId,
        Guid marketId,
        CancellationToken ct) =>
        db.Set<UserMarketAssignmentRow>()
            .Where(assignment =>
                assignment.UserId == agentUserId &&
                assignment.MarketId == marketId)
            .Join(
                db.Set<MarketAgentUserRow>().Where(user =>
                    user.IsActive &&
                    user.DeletedAt == null &&
                    user.RoleName == "market_agent"),
                assignment => assignment.UserId,
                user => user.Id,
                (assignment, _) => assignment)
            .AnyAsync(ct);
}
