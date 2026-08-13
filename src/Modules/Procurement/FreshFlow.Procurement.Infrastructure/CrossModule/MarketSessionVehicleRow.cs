namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class MarketSessionVehicleRow
{
    public Guid Id { get; set; }
    public Guid? HubId { get; set; }
    public decimal CapacityKg { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime? DeletedAt { get; set; }
}

internal sealed class ReservedRouteVehicleRow
{
    public Guid VehicleId { get; set; }
    public DateOnly ServiceDate { get; set; }
}
