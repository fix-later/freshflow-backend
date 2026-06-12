using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Pricing.Infrastructure.CrossModule;

/// <summary>
/// Reads assigned markets for a market agent by joining the user_market_assignments
/// and markets cross-module projections (no direct project references to Auth/Catalog).
/// </summary>
internal sealed class AssignedMarketReader(AppDbContext db) : IAssignedMarketReader
{
    public async Task<IReadOnlyList<AssignedMarketDto>> GetByAgentIdAsync(
        Guid agentUserId, CancellationToken ct)
    {
        var result = await db.Set<UserMarketAssignmentRow>()
            .Where(a => a.UserId == agentUserId)
            .Join(
                db.Set<MarketRow>().Where(m => m.IsActive),
                assignment => assignment.MarketId,
                market => market.Id,
                (_, market) => new AssignedMarketDto(market.Id, market.Name, market.Location, market.Address))
            .ToListAsync(ct);

        return result.AsReadOnly();
    }

    public Task<bool> HasAssignmentAsync(
        Guid agentUserId, Guid marketId, CancellationToken ct) =>
        db.Set<UserMarketAssignmentRow>()
            .Join(
                db.Set<MarketRow>().Where(m => m.IsActive),
                a => a.MarketId,
                m => m.Id,
                (a, _) => a)
            .AnyAsync(a => a.UserId == agentUserId && a.MarketId == marketId, ct);
}
