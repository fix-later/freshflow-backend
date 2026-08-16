namespace FreshFlow.Procurement.Domain.Entities;

public sealed class MarketSessionVehicle
{
    private MarketSessionVehicle() { }

    internal MarketSessionVehicle(Guid sessionId, Guid vehicleId, Guid? assignedBy, DateTime assignedAt)
    {
        SessionId = sessionId;
        VehicleId = vehicleId;
        AssignedBy = assignedBy;
        AssignedAt = assignedAt;
    }

    public Guid SessionId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid? AssignedBy { get; private set; }
    public DateTime AssignedAt { get; private set; }
}
