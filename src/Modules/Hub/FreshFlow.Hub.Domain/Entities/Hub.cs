namespace FreshFlow.Hub.Domain.Entities;

public sealed class Hub
{
    private Hub() { } // EF Core

    public Guid Id { get; private set; }
    // ponytail: nullable only while existing hubs are backfilled; make required in the follow-up migration.
    public Guid? MarketId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public decimal CapacityKg { get; private set; }
    public decimal OccupiedCapacityKg { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? ManagedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public decimal AvailableCapacityKg => CapacityKg - OccupiedCapacityKg;

    public static Hub Create(
        string name,
        string? address,
        decimal? latitude,
        decimal? longitude,
        decimal capacityKg,
        Guid? managedBy,
        Guid marketId)
    {
        Validate(name, latitude, longitude, capacityKg);
        if (marketId == Guid.Empty)
            throw new ArgumentException("Market ID cannot be empty.", nameof(marketId));

        var now = DateTime.UtcNow;
        return new Hub
        {
            Id = Guid.NewGuid(),
            MarketId = marketId,
            Name = name.Trim(),
            Address = NormalizeOptional(address),
            Latitude = latitude,
            Longitude = longitude,
            CapacityKg = capacityKg,
            OccupiedCapacityKg = 0,
            IsActive = true,
            ManagedBy = managedBy,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    // ponytail: internal overload is only for legacy test fixtures during the nullable-column rollout.
    internal static Hub Create(
        string name,
        string? address,
        decimal? latitude,
        decimal? longitude,
        decimal capacityKg,
        Guid? managedBy)
    {
        var hub = Create(
            name, address, latitude, longitude, capacityKg, managedBy, Guid.NewGuid());
        hub.MarketId = null;
        return hub;
    }

    public void Update(
        string name,
        string? address,
        decimal? latitude,
        decimal? longitude,
        decimal capacityKg,
        Guid? managedBy)
    {
        Validate(name, latitude, longitude, capacityKg);

        if (capacityKg < OccupiedCapacityKg)
            throw new InvalidOperationException("Capacity cannot be less than occupied capacity.");

        Name = name.Trim();
        Address = NormalizeOptional(address);
        Latitude = latitude;
        Longitude = longitude;
        CapacityKg = capacityKg;
        ManagedBy = managedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyInbound(decimal totalKg)
    {
        OccupiedCapacityKg += totalKg;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyOutbound(decimal totalKg)
    {
        if (totalKg <= 0)
            throw new ArgumentException("Outbound quantity must be greater than zero.", nameof(totalKg));

        OccupiedCapacityKg -= totalKg;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    private static void Validate(string name, decimal? latitude, decimal? longitude, decimal capacityKg)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Hub name is required.", nameof(name));

        if (capacityKg <= 0)
            throw new ArgumentException("Capacity must be greater than zero.", nameof(capacityKg));

        if (latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90.");

        if (longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180.");
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
