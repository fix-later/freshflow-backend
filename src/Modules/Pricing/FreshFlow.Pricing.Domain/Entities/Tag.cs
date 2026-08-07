using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Pricing.Domain.Entities;

/// <summary>
/// A catalog tag that market product listings may carry (SCRUM-386). Global — one shared
/// catalog across all markets. A tag with <see cref="PinsToTop"/> pins any listing that
/// carries it to the top of the market product board (see <c>MarketProductReader</c>).
/// </summary>
public sealed class Tag : AggregateRoot
{
    private Tag() { } // EF Core

    private Tag(string name, bool pinsToTop, Guid? actor)
    {
        Name = Normalize(name);
        PinsToTop = pinsToTop;
        UpdatedBy = actor;
    }

    public string Name { get; private set; } = string.Empty;
    public bool PinsToTop { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    private const int MaxNameLength = 30;

    public static Tag Create(string name, bool pinsToTop, Guid? actor) =>
        new(name, pinsToTop, actor);

    /// <summary>Renames the tag. Normalized (trim + lowercase-invariant). No-op if unchanged.</summary>
    public void Rename(string name, Guid? actor)
    {
        var normalized = Normalize(name);
        if (Name == normalized) return;

        Name = normalized;
        UpdatedBy = actor;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Toggles the pin-to-top flag. No-op if unchanged.</summary>
    public void SetPinsToTop(bool pinsToTop, Guid? actor)
    {
        if (PinsToTop == pinsToTop) return;

        PinsToTop = pinsToTop;
        UpdatedBy = actor;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        SoftDelete();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Trim + lowercase-invariant, the same rule applied on write. Used by callers
    /// (handlers) to look up an existing live tag by name before inserting/comparing.</summary>
    public static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name.Trim().ToLowerInvariant();
    }

    private static string Normalize(string name)
    {
        var normalized = NormalizeName(name);
        if (normalized.Length > MaxNameLength)
            throw new ArgumentException($"Tag name must be at most {MaxNameLength} characters.", nameof(name));

        return normalized;
    }
}
