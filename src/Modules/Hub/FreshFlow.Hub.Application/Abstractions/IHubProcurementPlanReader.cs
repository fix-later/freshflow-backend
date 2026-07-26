using FreshFlow.Hub.Application.Dtos;

namespace FreshFlow.Hub.Application.Abstractions;

public interface IHubProcurementPlanReader
{
    public Task<bool> HasOpenBatchesAsync(Guid hubId, CancellationToken ct);

    public Task<HubProcurementPlanDto> ReadAsync(
        Guid hubId,
        DateOnly date,
        CancellationToken ct);
}
