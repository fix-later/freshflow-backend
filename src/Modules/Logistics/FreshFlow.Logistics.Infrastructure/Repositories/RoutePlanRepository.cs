using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.Infrastructure.Repositories;

internal sealed class RoutePlanRepository(AppDbContext db) : IRoutePlanRepository
{
    public Task<RoutePlan?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<RoutePlan>().FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);

    public Task<RoutePlan?> FindProposedAsync(Guid hubId, DateOnly serviceDate, CancellationToken ct) =>
        db.Set<RoutePlan>().FirstOrDefaultAsync(x =>
            x.HubId == hubId && x.ServiceDate == serviceDate
            && x.Status == RoutePlanStatus.proposed && x.DeletedAt == null, ct);

    public async Task<IReadOnlyList<DeliveryRoute>> GetRoutesAsync(Guid planId, CancellationToken ct) =>
        await db.Set<DeliveryRoute>()
            .Where(x => x.RoutePlanId == planId && x.DeletedAt == null)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);

    public async Task AddAsync(RoutePlan plan, CancellationToken ct) =>
        await db.Set<RoutePlan>().AddAsync(plan, ct);

    public async Task<bool> TrySaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (DeliveryRouteRepository.IsUniqueViolation(ex))
        {
            return false;
        }
    }
}
