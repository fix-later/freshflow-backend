using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Abstractions;

public interface IProcurementBatchingService
{
    public Task<Result<BatchingResult>> BuildBatchesAsync(
        DateOnly batchDate,
        bool dryRun,
        bool force,
        CancellationToken ct);
}
