using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Procurement.Domain.Entities;

public sealed class MarketSession : AggregateRoot
{
    private readonly List<MarketSessionVehicle> _vehicles = [];
    private readonly List<MarketSessionAgent> _agents = [];

    private MarketSession() { }

    public Guid MarketId { get; private set; }
    public Guid? HubId { get; private set; }
    public DateOnly ServiceDate { get; private set; }
    public MarketSessionStatus Status { get; private set; }
    public DateTime ClosesAt { get; private set; }
    public MarketSessionCreatedSource CreatedSource { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public Guid? ClosedBy { get; private set; }
    public string? CloseReason { get; private set; }
    public DateTime? BatchingCompletedAt { get; private set; }
    public decimal? PlannedCapacityKg { get; private set; }
    public IReadOnlyCollection<MarketSessionVehicle> Vehicles => _vehicles.AsReadOnly();
    public IReadOnlyCollection<MarketSessionAgent> Agents => _agents.AsReadOnly();

    public static Result<MarketSession> Create(
        Guid marketId,
        Guid? hubId,
        DateOnly serviceDate,
        DateTime closesAtUtc,
        MarketSessionCreatedSource source,
        bool ready)
    {
        if (marketId == Guid.Empty || serviceDate == default || closesAtUtc.Kind != DateTimeKind.Utc)
        {
            return Result<MarketSession>.Failure(Error.Validation(
                "INVALID_MARKET_SESSION",
                "A market, service date, and UTC close time are required."));
        }
        if (ready && !hubId.HasValue)
            return Result<MarketSession>.Failure(Error.Validation(
                "HUB_NOT_CONFIGURED_FOR_MARKET", "A ready market session requires an active hub."));

        return Result<MarketSession>.Success(new MarketSession
        {
            MarketId = marketId,
            HubId = hubId,
            ServiceDate = serviceDate,
            ClosesAt = closesAtUtc,
            CreatedSource = source,
            Status = ready ? MarketSessionStatus.Open : MarketSessionStatus.Draft
        });
    }

    public Result UpdateSchedule(DateTime closesAtUtc, DateTime nowUtc)
    {
        if (Status == MarketSessionStatus.Closed)
            return ClosedConflict();
        if (closesAtUtc.Kind != DateTimeKind.Utc || closesAtUtc <= nowUtc)
            return Result.Failure(Error.Validation(
                "INVALID_MARKET_SESSION_CLOSE_TIME",
                "ClosesAt must be a future UTC timestamp."));

        ClosesAt = closesAtUtc;
        UpdatedAt = nowUtc;
        return Result.Success();
    }

    public Result Open(Guid hubId, DateTime nowUtc)
    {
        if (Status == MarketSessionStatus.Closed)
            return ClosedConflict();
        if (Status == MarketSessionStatus.Open)
            return Result.Success();
        if (ClosesAt <= nowUtc)
            return Result.Failure(Error.Conflict(
                "MARKET_SESSION_CUTOFF_PASSED",
                "A market session cannot be opened after its cutoff."));
        if (hubId == Guid.Empty)
            return Result.Failure(Error.Validation("HUB_NOT_CONFIGURED_FOR_MARKET", "An active hub is required."));

        HubId = hubId;
        Status = MarketSessionStatus.Open;
        UpdatedAt = nowUtc;
        return Result.Success();
    }

    public Result Close(Guid? actorId, string? reason, DateTime atUtc)
    {
        if (Status == MarketSessionStatus.Closed)
            return Result.Success();

        Status = MarketSessionStatus.Closed;
        ClosedAt = atUtc;
        ClosedBy = actorId;
        CloseReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        UpdatedAt = atUtc;
        return Result.Success();
    }

    public Result ConfigureResources(
        decimal plannedCapacityKg,
        IEnumerable<Guid> vehicleIds,
        IEnumerable<Guid> agentUserIds,
        Guid? actorId,
        DateTime atUtc)
    {
        if (Status == MarketSessionStatus.Closed)
            return ClosedConflict();

        var vehicles = vehicleIds?.Distinct().ToArray() ?? [];
        var agents = agentUserIds?.Distinct().ToArray() ?? [];
        if (plannedCapacityKg <= 0m || vehicles.Length == 0 || agents.Length == 0 ||
            vehicles.Contains(Guid.Empty) || agents.Contains(Guid.Empty))
        {
            return Result.Failure(Error.Validation(
                "INVALID_MARKET_SESSION_RESOURCES",
                "A positive capacity, at least one vehicle, and at least one market agent are required."));
        }

        PlannedCapacityKg = plannedCapacityKg;
        _vehicles.Clear();
        _vehicles.AddRange(vehicles.Select(id => new MarketSessionVehicle(Id, id, actorId, atUtc)));
        _agents.Clear();
        _agents.AddRange(agents.Select(id => new MarketSessionAgent(Id, id, actorId, atUtc)));
        UpdatedAt = atUtc;
        return Result.Success();
    }

    public void MarkBatchingCompleted(DateTime atUtc)
    {
        BatchingCompletedAt = atUtc;
        UpdatedAt = atUtc;
    }

    public void ResetBatching(DateTime atUtc)
    {
        BatchingCompletedAt = null;
        UpdatedAt = atUtc;
    }

    private static Result ClosedConflict() => Result.Failure(Error.Conflict(
        "MARKET_SESSION_CLOSED",
        "A closed market session cannot be changed."));
}
