using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Abstractions;

public interface IMarketSessionRepository
{
    public Task<Result> ExecuteInTransactionAsync(
        Func<CancellationToken, Task<Result>> operation, CancellationToken ct);
    public Task AddAsync(MarketSession session, CancellationToken ct);
    public Task<bool> SaveChangesAsync(CancellationToken ct);
    public Task<MarketSession?> FindByIdAsync(Guid id, CancellationToken ct);
    public Task<MarketSession?> FindForUpdateAsync(Guid id, CancellationToken ct);
    public Task<MarketSession?> FindByMarketAndDateAsync(Guid marketId, DateOnly serviceDate, CancellationToken ct);
    public Task<IReadOnlyList<MarketSession>> ListAsync(
        DateOnly? from, DateOnly? to, Guid? marketId, MarketSessionStatus? status, CancellationToken ct);
    public Task<IReadOnlySet<(Guid MarketId, DateOnly ServiceDate)>> ListKeysAsync(
        DateOnly from, DateOnly to, CancellationToken ct);
    public Task<IReadOnlyList<MarketSession>> ListDueAsync(DateTime nowUtc, CancellationToken ct);
    public Task<IReadOnlyList<MarketSession>> ListClosedPendingAsync(CancellationToken ct);
}
