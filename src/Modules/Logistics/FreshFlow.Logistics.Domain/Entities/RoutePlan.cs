using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;

namespace FreshFlow.Logistics.Domain.Entities;

public sealed class RoutePlan
{
    private RoutePlan() => Unassigned = [];

    private RoutePlan(
        Guid marketSessionId, Guid hubId, DateOnly serviceDate, OptimizationCriteria criteria,
        string routingProvider, bool isEstimated, string inputRevision,
        IReadOnlyList<RoutePlanUnassigned> unassigned, int vehiclesUsed,
        decimal totalLoadKg, decimal totalDistanceKm,
        int estimatedDurationMinutes, decimal estimatedCost)
    {
        Id = Guid.NewGuid();
        HubId = hubId;
        MarketSessionId = marketSessionId;
        ServiceDate = serviceDate;
        Status = RoutePlanStatus.proposed;
        OptimizationCriteria = criteria;
        RoutingProvider = routingProvider;
        IsEstimated = isEstimated;
        InputRevision = inputRevision;
        Unassigned = unassigned.ToList().AsReadOnly();
        VehiclesUsed = vehiclesUsed;
        TotalLoadKg = totalLoadKg;
        TotalDistanceKm = totalDistanceKm;
        EstimatedDurationMinutes = estimatedDurationMinutes;
        EstimatedCost = estimatedCost;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid MarketSessionId { get; private set; }
    public Guid HubId { get; private set; }
    public DateOnly ServiceDate { get; private set; }
    public RoutePlanStatus Status { get; private set; }
    public OptimizationCriteria OptimizationCriteria { get; private set; }
    public string RoutingProvider { get; private set; } = string.Empty;
    public bool IsEstimated { get; private set; }
    public string InputRevision { get; private set; } = string.Empty;
    public IReadOnlyList<RoutePlanUnassigned> Unassigned { get; private set; }
    public int VehiclesUsed { get; private set; }
    public decimal TotalLoadKg { get; private set; }
    public decimal TotalDistanceKm { get; private set; }
    public int EstimatedDurationMinutes { get; private set; }
    public decimal EstimatedCost { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public static RoutePlan Create(
        Guid marketSessionId, Guid hubId, DateOnly serviceDate, OptimizationCriteria criteria,
        string routingProvider, bool isEstimated, string inputRevision,
        IReadOnlyList<RoutePlanUnassigned> unassigned, int vehiclesUsed,
        decimal totalLoadKg, decimal totalDistanceKm,
        int estimatedDurationMinutes, decimal estimatedCost)
    {
        if (marketSessionId == Guid.Empty)
            throw new ArgumentException("Market session id is required.", nameof(marketSessionId));
        if (hubId == Guid.Empty)
            throw new ArgumentException("Hub id is required.", nameof(hubId));
        if (string.IsNullOrWhiteSpace(routingProvider))
            throw new ArgumentException("Routing provider is required.", nameof(routingProvider));
        if (string.IsNullOrWhiteSpace(inputRevision))
            throw new ArgumentException("Input revision is required.", nameof(inputRevision));

        return new RoutePlan(
            marketSessionId, hubId, serviceDate, criteria, routingProvider, isEstimated, inputRevision,
            unassigned, vehiclesUsed, totalLoadKg, totalDistanceKm,
            estimatedDurationMinutes, estimatedCost);
    }

    public void Approve()
    {
        EnsureProposed();
        Status = RoutePlanStatus.approved;
        ApprovedAt = DateTime.UtcNow;
    }

    public void MarkStale()
    {
        EnsureProposed();
        Status = RoutePlanStatus.stale;
    }

    public void Supersede()
    {
        EnsureProposed();
        Status = RoutePlanStatus.superseded;
    }

    private void EnsureProposed()
    {
        if (Status != RoutePlanStatus.proposed)
            throw new InvalidOperationException("Only a proposed route plan can be changed.");
    }
}
