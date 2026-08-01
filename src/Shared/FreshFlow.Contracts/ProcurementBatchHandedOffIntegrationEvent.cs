using MediatR;

namespace FreshFlow.Contracts;

public sealed record ProcurementBatchHandedOffIntegrationEvent(
    Guid BatchId,
    Guid MarketId,
    Guid? HubId,
    DateTime HandedOffAt,
    IReadOnlyList<Guid> CoveredOrderIds,
    Guid? HandedOffByUserId = null,
    IReadOnlyList<ProcurementPurchaseActual>? PurchaseActuals = null) : INotification;

public sealed record ProcurementPurchaseActual(
    Guid MarketProductId,
    int ActualQuantity,
    decimal? ActualUnitPrice);

public sealed class ProcurementHandoverRejectedException(string code, string message)
    : Exception(message)
{
    public string Code { get; } = code;
}

public interface IProcurementHandoverOrderFinalizer
{
    public Task<IReadOnlyList<INotification>> FinalizeAsync(
        ProcurementBatchHandedOffIntegrationEvent notification,
        CancellationToken cancellationToken);
}
