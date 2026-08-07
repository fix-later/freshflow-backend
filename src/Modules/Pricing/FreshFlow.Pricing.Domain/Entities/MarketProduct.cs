using FreshFlow.Pricing.Domain.Events;
using FreshFlow.SharedKernel.Domain;

namespace FreshFlow.Pricing.Domain.Entities;

public sealed class MarketProduct : AggregateRoot
{
    private MarketProduct() { } // EF Core

    public MarketProduct(Guid marketId, Guid productId, decimal initialPrice, int initialQuantity, Guid? createdBy)
    {
        ValidatePrice(initialPrice);
        ValidateQuantity(initialQuantity);

        MarketId = marketId;
        ProductId = productId;
        CurrentPrice = initialPrice;
        CurrentQuantity = initialQuantity;
        ReservedQuantity = 0;
        UpdatedBy = createdBy;
    }

    public Guid MarketId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal CurrentPrice { get; private set; }
    public int CurrentQuantity { get; private set; }
    public int ReservedQuantity { get; private set; }

    /// <summary>
    /// Free-form tags. Normalized (trimmed, lowercased, deduped) on write via <see cref="SetTags"/>.
    /// CLR type is <see cref="List{T}"/> (not <see cref="IReadOnlyList{T}"/>) so it maps directly to
    /// Postgres <c>text[]</c> and LINQ queries (<c>Tags.Contains(...)</c>) translate to <c>@&gt;</c>.
    /// </summary>
    public List<string> Tags { get; private set; } = [];

    public Guid? UpdatedBy { get; private set; }

    /// <summary>Tag value that pins a listing to the top of the market product board.</summary>
    public const string FeaturedTag = "nổi bật";

    private const int MaxTags = 8;
    private const int MaxTagLength = 30;

    public bool IsFeatured => Tags.Contains(FeaturedTag);

    public int AvailableQuantity => CurrentQuantity - ReservedQuantity;
    public bool IsOutOfStock => CurrentQuantity == 0;

    public void Delete()
    {
        SoftDelete();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Replaces the tag set. Each tag is trimmed, lowercased (invariant), empties are dropped,
    /// and duplicates removed. No-op (no concurrency bump) when the normalized set is unchanged —
    /// mirrors the old <c>SetFeatured</c> guard. <see cref="FeaturedTag"/> pins the listing to the
    /// top of the market product board (see <c>MarketProductReader</c>).
    /// </summary>
    public void SetTags(IEnumerable<string> tags, Guid? actor)
    {
        var normalized = NormalizeTags(tags);
        if (Tags.Count == normalized.Count && Tags.ToHashSet().SetEquals(normalized))
            return;

        Tags = normalized;
        UpdatedBy = actor;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies a price-only update. Raises <see cref="PriceUpdatedDomainEvent"/>.
    /// Caller must ensure <paramref name="newPrice"/> is positive (domain defends in depth).
    /// </summary>
    public void UpdatePrice(decimal newPrice, Guid? actor)
    {
        ValidatePrice(newPrice);

        var oldPrice = CurrentPrice;
        CurrentPrice = newPrice;
        UpdatedBy = actor;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new PriceUpdatedDomainEvent(
            Id, MarketId, ProductId, oldPrice, CurrentPrice, CurrentQuantity, actor, DateTime.UtcNow));
    }

    /// <summary>
    /// Dedicated quantity-only update (UC-PRI-04).
    /// Delegates to <see cref="ApplyUpdate"/> so exactly one <see cref="PriceUpdatedDomainEvent"/>
    /// is raised with the full context required by downstream handlers.
    ///
    /// Caller (handler) must pre-validate quantity ≥ 0; domain defends in depth.
    /// </summary>
    public void UpdateAvailableQuantity(int newQuantity, Guid? actor) =>
        ApplyUpdate(newPrice: null, newQuantity: newQuantity, actor: actor);

    /// <summary>
    /// Combined update of price and/or quantity in a single operation.
    /// Raises exactly one <see cref="PriceUpdatedDomainEvent"/> carrying the full context
    /// needed by downstream handlers (SignalR broadcast, Redis sync, price history).
    ///
    /// The handler (UC-PRI-03) pre-validates business rules before calling this method.
    /// Domain-level guards are retained as a defence-in-depth layer.
    /// </summary>
    public void ApplyUpdate(decimal? newPrice, int? newQuantity, Guid? actor)
    {
        // Guard: a call with neither field set is a no-op.
        // Do NOT mutate UpdatedBy/UpdatedAt or raise a spurious domain event.
        if (!newPrice.HasValue && !newQuantity.HasValue)
            return;

        var previousPrice = CurrentPrice;

        if (newPrice.HasValue)
        {
            ValidatePrice(newPrice.Value);
            CurrentPrice = newPrice.Value;
        }

        if (newQuantity.HasValue)
        {
            ValidateQuantity(newQuantity.Value);
            CurrentQuantity = newQuantity.Value;
        }

        UpdatedBy = actor;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new PriceUpdatedDomainEvent(
            Id, MarketId, ProductId, previousPrice, CurrentPrice, CurrentQuantity, actor, DateTime.UtcNow));
    }

    // ── Invariant helpers ────────────────────────────────────────────────────

    private static void ValidatePrice(decimal price)
    {
        if (price <= 0)
            throw new ArgumentOutOfRangeException(nameof(price), price, "Price must be greater than zero.");
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity < 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be non-negative.");
    }

    private static List<string> NormalizeTags(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var normalized = tags
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length > 0)
            .Distinct()
            .ToList();

        if (normalized.Count > MaxTags)
            throw new ArgumentException($"A market product may carry at most {MaxTags} tags.", nameof(tags));

        if (normalized.Any(t => t.Length > MaxTagLength))
            throw new ArgumentException($"Each tag must be at most {MaxTagLength} characters.", nameof(tags));

        return normalized;
    }
}
