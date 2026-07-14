using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshFlow.Procurement.Infrastructure.Repositories;

internal sealed class ProcurementBatchRepository(AppDbContext db) : IProcurementBatchRepository
{
    public Task AddRangeAsync(
        IReadOnlyCollection<ProcurementBatch> batches,
        CancellationToken ct) =>
        db.Set<ProcurementBatch>().AddRangeAsync(batches, ct);

    public async Task<bool> SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_procurement_batch_orders_order_active"
            })
        {
            return false;
        }
    }

    public Task<ProcurementBatch?> FindByIdAsync(Guid batchId, CancellationToken ct) =>
        db.Set<ProcurementBatch>()
            .Include(batch => batch.Items)
            .Include(batch => batch.Orders)
            .Include(batch => batch.Exceptions)
            .SingleOrDefaultAsync(
                batch => batch.Id == batchId && batch.DeletedAt == null,
                ct);

    public Task<bool> CycleExistsAsync(DateOnly batchDate, CancellationToken ct) =>
        db.Set<ProcurementBatch>()
            .AsNoTracking()
            .AnyAsync(
                batch => batch.BatchDate == batchDate && batch.DeletedAt == null,
                ct);

    public async Task<(IReadOnlyList<ProcurementBatch> Batches, int Total)> ListAsync(
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var query = db.Set<ProcurementBatch>()
            .AsNoTracking()
            .Include(batch => batch.Items)
            .Include(batch => batch.Orders)
            .Include(batch => batch.Exceptions)
            .Where(batch => batch.DeletedAt == null);

        var total = await query.CountAsync(ct);
        var result = await query
            .OrderByDescending(batch => batch.BatchDate)
            .ThenBy(batch => batch.MarketId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (result.AsReadOnly(), total);
    }

    public async Task<(IReadOnlyList<ProcurementBatch> Batches, int Total)> ListByAgentAsync(
        Guid agentUserId,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var query = db.Set<ProcurementBatch>()
            .AsNoTracking()
            .Include(batch => batch.Items)
            .Include(batch => batch.Orders)
            .Include(batch => batch.Exceptions)
            .Where(batch =>
                batch.DeletedAt == null &&
                batch.AssignedAgentUserId == agentUserId);

        var total = await query.CountAsync(ct);
        var result = await query
            .OrderByDescending(batch => batch.BatchDate)
            .ThenBy(batch => batch.MarketId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (result.AsReadOnly(), total);
    }

    public Task<DateOnly?> GetLatestCycleDateAsync(CancellationToken ct) =>
        db.Set<ProcurementBatch>()
            .AsNoTracking()
            .Where(batch => batch.DeletedAt == null)
            .Select(batch => (DateOnly?)batch.BatchDate)
            .MaxAsync(ct);

    public async Task<IReadOnlyList<ProcurementBatch>> ListByDateAsync(
        DateOnly date,
        CancellationToken ct)
    {
        var result = await db.Set<ProcurementBatch>()
            .AsNoTracking()
            .Include(batch => batch.Items)
            .Include(batch => batch.Orders)
            .Include(batch => batch.Exceptions)
            .Where(batch => batch.DeletedAt == null && batch.BatchDate == date)
            .OrderByDescending(batch => batch.BatchDate)
            .ThenBy(batch => batch.MarketId)
            .ToListAsync(ct);

        return result.AsReadOnly();
    }
}
