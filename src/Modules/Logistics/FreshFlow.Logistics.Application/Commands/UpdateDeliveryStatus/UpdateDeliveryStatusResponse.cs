namespace FreshFlow.Logistics.Application.Commands.UpdateDeliveryStatus;

public sealed record UpdateDeliveryStatusResponse(
    Guid DeliveryId,
    Guid RouteId,
    string Status,
    DateTime? ActualArrival,
    string? FailureReason);
