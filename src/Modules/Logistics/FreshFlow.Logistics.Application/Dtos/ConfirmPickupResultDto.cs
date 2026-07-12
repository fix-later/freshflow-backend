namespace FreshFlow.Logistics.Application.Dtos;

public sealed record ConfirmPickupResultDto(Guid RouteId, IReadOnlyList<Guid> DeliveryIds);
