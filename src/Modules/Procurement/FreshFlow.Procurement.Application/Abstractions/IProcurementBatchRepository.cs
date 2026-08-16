using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Abstractions;

public interface IProcurementBatchRepository
{
    public Task<Result> ExecuteInSerializableTransactionAsync(
        Func<CancellationToken, Task<Result>> operation,
        CancellationToken ct);

    public Task AddRangeAsync(
        IReadOnlyCollection<ProcurementBatch> batches,
        CancellationToken ct);

    public Task<bool> SaveChangesAsync(CancellationToken ct);

    public Task<ProcurementBatch?> FindByIdAsync(Guid batchId, CancellationToken ct);

    public Task<ProcurementBatch?> FindByOrderIdAsync(Guid orderId, CancellationToken ct);

    public Task<bool> CycleExistsAsync(DateOnly batchDate, CancellationToken ct);

    public Task<int> CountByMarketAndDateAsync(
        Guid marketId,
        DateOnly batchDate,
        CancellationToken ct);

    public Task<(IReadOnlyList<ProcurementBatch> Batches, int Total)> ListAsync(
        int page,
        int pageSize,
        DateOnly? date,
        Guid? marketId,
        CancellationToken ct);

    public Task<(IReadOnlyList<ProcurementBatch> Batches, int Total)> ListByAgentAsync(
        Guid agentUserId,
        int page,
        int pageSize,
        CancellationToken ct);

    public Task<DateOnly?> GetLatestCycleDateAsync(CancellationToken ct);

    public Task<IReadOnlyList<ProcurementBatch>> ListByDateAsync(
        DateOnly date,
        CancellationToken ct);

    public Task<IReadOnlyList<ProcurementBatch>> ListMergeableByDateAsync(
        DateOnly date,
        CancellationToken ct);

    public Task<Result<BatchingResetCounts>> ResetDayAsync(
        DateOnly batchDate,
        DateTime resetAtUtc,
        CancellationToken ct);
}
