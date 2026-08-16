using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Abstractions;

/// <summary>
/// Pricing → credit → cutoff-evaluation → stock-reservation → charge pipeline shared by a manual
/// confirm (<c>ConfirmOrderCommandHandler</c>) and the scheduled-order auto-confirm job
/// (<c>ScheduledOrderGenerationService</c>, SCRUM-386).
/// </summary>
public interface IOrderConfirmationService
{
    public Task<Result<RoadDistanceResult>> GetRoadDistanceAsync(
        IReadOnlyCollection<Guid> marketProductIds,
        Guid restaurantId,
        Guid deliveryAddressId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Callers own restaurant resolution/authorization and must wrap this call in
    /// <see cref="IOrderRepository.ExecuteInSerializableTransactionAsync"/>.
    /// </summary>
    public Task<Result<OrderDto>> ConfirmAsync(
        Order order,
        Guid restaurantId,
        Guid deliveryAddressId,
        RoadDistanceResult roadDistance,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}
