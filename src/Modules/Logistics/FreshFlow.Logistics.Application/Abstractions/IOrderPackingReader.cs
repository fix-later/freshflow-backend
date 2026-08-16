using FreshFlow.Logistics.Application.Dtos;

namespace FreshFlow.Logistics.Application.Abstractions;

public interface IOrderPackingReader
{
    public Task<IReadOnlyList<OrderPackingLine>> GetLinesAsync(
        Guid orderId, CancellationToken ct);

    public Task<IReadOnlyList<OrderPackingLines>> GetLinesByOrdersAsync(
        IReadOnlyCollection<Guid> orderIds, CancellationToken ct);
}
