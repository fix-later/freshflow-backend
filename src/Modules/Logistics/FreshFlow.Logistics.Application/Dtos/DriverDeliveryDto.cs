namespace FreshFlow.Logistics.Application.Dtos;

public sealed record DriverDeliveryDto(
    Guid DeliveryId,
    Guid OrderId,
    int SequenceNumber,
    string Status,
    DateTime? EstimatedArrival,
    DateTime? ActualArrival,
    string? ProofUrl);
