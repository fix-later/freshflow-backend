namespace FreshFlow.Analytics.Infrastructure.CrossModule;

internal sealed class DeliveryRow
{
    public Guid DeliveryId { get; init; }
    public Guid DeliveryRouteId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? EstimatedArrival { get; init; }
    public DateTime? ActualArrival { get; init; }
    public DateTime UpdatedAt { get; init; }
}

