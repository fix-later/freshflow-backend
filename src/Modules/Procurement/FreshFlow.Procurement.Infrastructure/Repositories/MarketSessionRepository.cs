using System.Data;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshFlow.Procurement.Infrastructure.Repositories;

internal sealed class MarketSessionRepository(AppDbContext db) : IMarketSessionRepository
{
    public async Task<Result> ExecuteInTransactionAsync(
        Func<CancellationToken, Task<Result>> operation, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
            return await operation(ct);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var result = await operation(ct);
            if (result.IsFailure)
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                return result;
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            return Result.Failure(Error.Conflict(
                "MARKET_SESSION_CONFLICT", "The market session changed concurrently. Retry."));
        }
    }

    public Task AddAsync(MarketSession session, CancellationToken ct) =>
        db.Set<MarketSession>().AddAsync(session, ct).AsTask();

    public async Task<bool> SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_market_sessions_market_date_active"
        })
        {
            db.ChangeTracker.Clear();
            return false;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public Task<MarketSession?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<MarketSession>()
            .Include(session => session.Vehicles)
            .Include(session => session.Agents)
            .SingleOrDefaultAsync(
            session => session.Id == id && session.DeletedAt == null, ct);

    public Task<MarketSession?> FindForUpdateAsync(Guid id, CancellationToken ct) =>
        db.Set<MarketSession>()
            .FromSqlInterpolated($"SELECT * FROM market_sessions WHERE id = {id} AND deleted_at IS NULL FOR UPDATE")
            .Include(session => session.Vehicles)
            .Include(session => session.Agents)
            .SingleOrDefaultAsync(ct);

    public Task<MarketSession?> FindByMarketAndDateAsync(
        Guid marketId, DateOnly serviceDate, CancellationToken ct) =>
        db.Set<MarketSession>()
            .Include(session => session.Vehicles)
            .Include(session => session.Agents)
            .SingleOrDefaultAsync(session =>
            session.MarketId == marketId &&
            session.ServiceDate == serviceDate &&
            session.DeletedAt == null, ct);

    public async Task<IReadOnlyList<MarketSession>> ListAsync(
        DateOnly? from,
        DateOnly? to,
        Guid? marketId,
        MarketSessionStatus? status,
        CancellationToken ct)
    {
        var query = db.Set<MarketSession>().AsNoTracking()
            .Include(session => session.Vehicles)
            .Include(session => session.Agents)
            .Where(session => session.DeletedAt == null);
        if (from.HasValue) query = query.Where(session => session.ServiceDate >= from.Value);
        if (to.HasValue) query = query.Where(session => session.ServiceDate <= to.Value);
        if (marketId.HasValue) query = query.Where(session => session.MarketId == marketId.Value);
        if (status.HasValue) query = query.Where(session => session.Status == status.Value);

        return await query
            .OrderBy(session => session.ServiceDate)
            .ThenBy(session => session.MarketId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlySet<(Guid MarketId, DateOnly ServiceDate)>> ListKeysAsync(
        DateOnly from, DateOnly to, CancellationToken ct) =>
        (await db.Set<MarketSession>()
            .AsNoTracking()
            .Where(session => session.DeletedAt == null &&
                              session.ServiceDate >= from && session.ServiceDate <= to)
            .Select(session => new { session.MarketId, session.ServiceDate })
            .ToListAsync(ct))
        .Select(row => (row.MarketId, row.ServiceDate))
        .ToHashSet();

    public async Task<IReadOnlyList<MarketSession>> ListDueAsync(DateTime nowUtc, CancellationToken ct) =>
        await db.Set<MarketSession>()
            .Where(session => session.DeletedAt == null &&
                              session.Status != MarketSessionStatus.Closed &&
                              session.ClosesAt <= nowUtc)
            .OrderBy(session => session.ClosesAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MarketSession>> ListClosedPendingAsync(CancellationToken ct) =>
        await db.Set<MarketSession>()
            .Where(session => session.DeletedAt == null &&
                              session.Status == MarketSessionStatus.Closed &&
                              session.BatchingCompletedAt == null)
            .OrderBy(session => session.ServiceDate)
            .ThenBy(session => session.MarketId)
            .ToListAsync(ct);
}
