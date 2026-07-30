using System.Data;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.Procurement.Infrastructure.CrossModule;
using FreshFlow.SharedKernel.Application;
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
        DateOnly? date,
        Guid? marketId,
        CancellationToken ct)
    {
        var query = db.Set<ProcurementBatch>()
            .AsNoTracking()
            .Include(batch => batch.Items)
            .Include(batch => batch.Orders)
            .Include(batch => batch.Exceptions)
            .Where(batch => batch.DeletedAt == null);

        if (date is not null)
        {
            query = query.Where(batch => batch.BatchDate == date.Value);
        }

        if (marketId is not null)
        {
            query = query.Where(batch => batch.MarketId == marketId.Value);
        }

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

    public async Task<Result<BatchingResetCounts>> ResetDayAsync(
        DateOnly batchDate,
        DateTime resetAtUtc,
        CancellationToken ct)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);

            var batches = await db.Set<ProcurementBatch>()
                .AsNoTracking()
                .Where(batch => batch.BatchDate == batchDate && batch.DeletedAt == null)
                .Select(batch => new { batch.Id, batch.Status })
                .ToListAsync(ct);

            if (batches.Count == 0)
            {
                await transaction.CommitAsync(ct);
                return Result<BatchingResetCounts>.Success(new BatchingResetCounts(0, 0));
            }

            if (batches.Any(batch =>
                    batch.Status is not ProcurementBatchStatus.Built
                        and not ProcurementBatchStatus.Manifested))
            {
                return NotAllowed(batchDate);
            }

            var batchIds = batches.Select(batch => batch.Id).ToArray();
            var orderIds = await db.Set<ProcurementBatchOrder>()
                .AsNoTracking()
                .Where(link =>
                    batchIds.Contains(link.ProcurementBatchId) &&
                    link.DeletedAt == null)
                .Select(link => link.OrderId)
                .Distinct()
                .ToArrayAsync(ct);

            if (orderIds.Length == 0)
                return NotAllowed(batchDate);

            var orderStates = await db.Set<ConfirmedOrderRow>()
                .AsNoTracking()
                .Where(order => orderIds.Contains(order.Id))
                .Select(order => new { order.Id, order.Status, order.DeletedAt })
                .ToListAsync(ct);

            if (orderStates.Count != orderIds.Length ||
                orderStates.Any(order =>
                    order.DeletedAt != null ||
                    order.Status is not "Confirmed" and not "Batched"))
            {
                return NotAllowed(batchDate);
            }

            var ordersReset = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE orders
                SET "Status" = 'Confirmed',
                    "UpdatedAt" = {resetAtUtc}
                WHERE "Id" = ANY ({orderIds})
                  AND "Status" = 'Batched'
                  AND deleted_at IS NULL
                """, ct);

            await SoftDeleteAsync<ProcurementException>(batchIds, resetAtUtc, ct);
            await SoftDeleteAsync<ProcurementBatchItem>(batchIds, resetAtUtc, ct);
            await SoftDeleteAsync<ProcurementBatchOrder>(batchIds, resetAtUtc, ct);
            await db.Set<ProcurementBatch>()
                .Where(batch => batchIds.Contains(batch.Id) && batch.DeletedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(batch => batch.DeletedAt, resetAtUtc)
                    .SetProperty(batch => batch.UpdatedAt, resetAtUtc), ct);

            await transaction.CommitAsync(ct);
            return Result<BatchingResetCounts>.Success(
                new BatchingResetCounts(batches.Count, ordersReset));
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            return Result<BatchingResetCounts>.Failure(Error.Conflict(
                "BATCH_RESET_NOT_ALLOWED",
                $"Procurement batches for '{batchDate:yyyy-MM-dd}' changed concurrently. Retry the reset."));
        }
    }

    private Task<int> SoftDeleteAsync<TEntity>(
        IReadOnlyCollection<Guid> batchIds,
        DateTime resetAtUtc,
        CancellationToken ct)
        where TEntity : FreshFlow.SharedKernel.Domain.BaseEntity =>
        db.Set<TEntity>()
            .Where(entity =>
                batchIds.Contains(EF.Property<Guid>(entity, "ProcurementBatchId")) &&
                entity.DeletedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(entity => entity.DeletedAt, resetAtUtc)
                .SetProperty(entity => entity.UpdatedAt, resetAtUtc), ct);

    private static Result<BatchingResetCounts> NotAllowed(DateOnly batchDate) =>
        Result<BatchingResetCounts>.Failure(Error.Conflict(
            "BATCH_RESET_NOT_ALLOWED",
            $"Procurement batches for '{batchDate:yyyy-MM-dd}' cannot be reset because their batches or orders have moved past batching."));
}
