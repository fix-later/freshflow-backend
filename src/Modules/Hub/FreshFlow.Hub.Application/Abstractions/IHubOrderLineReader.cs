using FreshFlow.Hub.Application.Dtos;

namespace FreshFlow.Hub.Application.Abstractions;

public interface IHubOrderLineReader
{
    public Task<IReadOnlyList<HubOrderLineDto>> GetLinesByOrdersAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken ct);
}
