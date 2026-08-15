namespace FreshFlow.Procurement.Application.Abstractions;

public interface IConfirmedOrderReader
{
    public Task<IReadOnlyList<ConfirmedOrderDto>> ReadEligibleAsync(
        DateOnly batchDate,
        bool force,
        CancellationToken ct);

    public Task<IReadOnlyList<ConfirmedOrderDto>> ReadEligibleForSessionAsync(
        Guid marketSessionId, CancellationToken ct);

    public Task<DateOnly?> FindOldestEligibleCycleAsync(DateOnly throughDate, CancellationToken ct);

    public Task<IReadOnlyDictionary<Guid, string>> ReadStatusesAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken ct);

    public Task<IReadOnlyDictionary<Guid, string>> ReadRestaurantNamesAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken ct);

    public Task<IReadOnlyList<ConfirmedOrderItemCostDto>> ReadItemCostsAsync(
        IReadOnlyCollection<Guid> orderIds,
        CancellationToken ct);
}

public sealed record ConfirmedOrderDto(
    Guid Id,
    DateTime ScheduledFor,
    IReadOnlyList<ConfirmedOrderItemDto> Items);

public sealed record ConfirmedOrderItemDto(
    Guid MarketProductId,
    string ProductNameSnapshot,
    int Quantity);

public sealed record ConfirmedOrderItemCostDto(
    Guid OrderId,
    Guid MarketProductId,
    decimal RestaurantOrderTotal);
