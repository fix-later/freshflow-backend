using FreshFlow.Procurement.Domain.Entities;

namespace FreshFlow.Procurement.Application.Abstractions;

public interface IProcurementBatchRepository
{
    public Task AddRangeAsync(
        IReadOnlyCollection<ProcurementBatch> batches,
        CancellationToken ct);

    public Task<bool> SaveChangesAsync(CancellationToken ct);

    public Task<ProcurementBatch?> FindByIdAsync(Guid batchId, CancellationToken ct);

    public Task<bool> CycleExistsAsync(DateOnly batchDate, CancellationToken ct);

    public Task<(IReadOnlyList<ProcurementBatch> Batches, int Total)> ListAsync(
        int page,
        int pageSize,
        CancellationToken ct);

    public Task<(IReadOnlyList<ProcurementBatch> Batches, int Total)> ListByAgentAsync(
        Guid agentUserId,
        int page,
        int pageSize,
        CancellationToken ct);
}
