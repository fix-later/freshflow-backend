namespace FreshFlow.Procurement.Application.Abstractions;

public interface IHubByMarketReader
{
    public Task<bool> IsActiveAsync(Guid hubId, CancellationToken ct);

    public Task<IReadOnlyDictionary<Guid, Guid>> ReadActiveHubsAsync(
        IReadOnlyCollection<Guid> marketIds,
        CancellationToken ct);
}
