namespace FreshFlow.Logistics.Infrastructure.CrossModule;

internal sealed class MarketSessionVehicleAssignmentRow
{
    public Guid HubId { get; set; }
    public DateOnly ServiceDate { get; set; }
    public Guid VehicleId { get; set; }
}
