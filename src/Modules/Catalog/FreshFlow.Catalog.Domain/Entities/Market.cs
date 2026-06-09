using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Catalog.Domain.Entities;

public sealed class Market : AggregateRoot
{
    private Market() { } // EF Core

    public Market(string name, string? location, string? address, decimal? latitude, decimal? longitude)
    {
        ValidateName(name);
        ValidateCoordinates(latitude, longitude);

        Name = name;
        Location = location;
        Address = address;
        Latitude = latitude;
        Longitude = longitude;
        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;
    public string? Location { get; private set; }
    public string? Address { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Update(string name, string? location, string? address, decimal? latitude, decimal? longitude)
    {
        ValidateName(name);
        ValidateCoordinates(latitude, longitude);

        Name = name;
        Location = location;
        Address = address;
        Latitude = latitude;
        Longitude = longitude;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        SoftDelete();
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    // ── Invariant helpers ────────────────────────────────────────────────────

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Market name must not be blank.", nameof(name));
    }

    private static void ValidateCoordinates(decimal? latitude, decimal? longitude)
    {
        if (latitude is not null && (latitude < -90 || latitude > 90))
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude,
                "Latitude must be between -90 and 90.");

        if (longitude is not null && (longitude < -180 || longitude > 180))
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude,
                "Longitude must be between -180 and 180.");
    }
}
