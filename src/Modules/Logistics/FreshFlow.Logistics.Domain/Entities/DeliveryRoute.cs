using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;

namespace FreshFlow.Logistics.Domain.Entities;

public sealed class DeliveryRoute
{
    private DeliveryRoute()
    {
        Stops = [];
    }

    private DeliveryRoute(DateOnly serviceDate, IReadOnlyList<RouteStop> stops, Guid? createdBy)
    {
        Id = Guid.NewGuid();
        RouteType = RouteType.direct;
        Status = RouteStatus.planned;
        ServiceDate = serviceDate;
        Stops = stops.ToList().AsReadOnly();
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public RouteType RouteType { get; private set; }
    public RouteStatus Status { get; private set; }
    public DateOnly ServiceDate { get; private set; }
    public IReadOnlyList<RouteStop> Stops { get; private set; }
    public decimal? TotalDistanceKm { get; private set; }
    public int? EstimatedDurationMinutes { get; private set; }
    public decimal? EstimatedCost { get; private set; }
    public OptimizationCriteria? OptimizationCriteria { get; private set; }
    public Guid? VehicleId { get; private set; }
    public Guid? DriverUserId { get; private set; }
    public Guid? OrderGroupId { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public static DeliveryRoute CreateDirect(
        DateOnly serviceDate,
        IReadOnlyList<RouteStop> stops,
        Guid? createdBy)
    {
        ArgumentNullException.ThrowIfNull(stops);

        if (stops.Count > 20)
            throw new ArgumentException("A delivery route cannot contain more than 20 stops.", nameof(stops));

        if (!stops.Any(stop => stop.EntityType == StopEntityType.market))
            throw new ArgumentException("A direct delivery route requires at least one market stop.", nameof(stops));

        if (!stops.Any(stop => stop.EntityType == StopEntityType.restaurant))
            throw new ArgumentException("A direct delivery route requires at least one restaurant stop.", nameof(stops));

        return new DeliveryRoute(serviceDate, stops, createdBy);
    }

    public void Select()
    {
        if (Status != RouteStatus.planned)
            throw new InvalidOperationException("Only planned routes can be selected.");

        Status = RouteStatus.selected;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyOptimization(
        IReadOnlyList<RouteStop> optimizedStops,
        decimal totalDistanceKm,
        int estimatedDurationMinutes,
        decimal estimatedCost,
        OptimizationCriteria criteria)
    {
        if (Status != RouteStatus.planned && Status != RouteStatus.selected)
            throw new InvalidOperationException("Only planned or selected routes can be optimized.");

        ArgumentNullException.ThrowIfNull(optimizedStops);

        Stops = optimizedStops.ToList().AsReadOnly();
        TotalDistanceKm = totalDistanceKm;
        EstimatedDurationMinutes = estimatedDurationMinutes;
        EstimatedCost = estimatedCost;
        OptimizationCriteria = criteria;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AdjustStopOrder(IReadOnlyList<Guid> orderedEntityIds)
    {
        if (Status != RouteStatus.selected)
            throw new InvalidOperationException("Only selected routes can have their stop order adjusted.");

        ArgumentNullException.ThrowIfNull(orderedEntityIds);

        var currentIds = Stops.Select(stop => stop.EntityId).ToHashSet();
        var newIds = orderedEntityIds.ToHashSet();

        if (orderedEntityIds.Count != Stops.Count || !currentIds.SetEquals(newIds))
        {
            throw new ArgumentException(
                "StopOrder must be a permutation of the route's existing stop entity ids.",
                nameof(orderedEntityIds));
        }

        var byEntityId = Stops.ToDictionary(stop => stop.EntityId);
        Stops = orderedEntityIds
            .Select((id, index) => byEntityId[id] with { StopOrder = index })
            .ToList()
            .AsReadOnly();
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkReviewed()
    {
        if (Status != RouteStatus.selected)
            throw new InvalidOperationException("Only selected routes can be reviewed.");

        if (OptimizationCriteria is null)
            throw new InvalidOperationException("Route must be optimized before it can be reviewed.");

        Status = RouteStatus.reviewed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Assign(Guid vehicleId, Guid? driverUserId)
    {
        if (Status == RouteStatus.assigned)
        {
            if (VehicleId == vehicleId && DriverUserId == driverUserId)
                return;

            throw new InvalidOperationException(
                "Route is already assigned to a different vehicle or driver.");
        }

        if (Status != RouteStatus.reviewed)
            throw new InvalidOperationException("Only reviewed routes can be assigned a vehicle.");

        VehicleId = vehicleId;
        DriverUserId = driverUserId;
        Status = RouteStatus.assigned;
        UpdatedAt = DateTime.UtcNow;
    }
}
