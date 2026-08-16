using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Abstractions;

public interface IProcurementBatchingService
{
    public Task<Result<BatchingResult>> BuildSessionBatchAsync(
        Guid marketSessionId,
        bool dryRun,
        CancellationToken ct);
}
