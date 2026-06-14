using FluentAssertions;
using FreshFlow.Pricing.Domain.Entities;

namespace FreshFlow.Pricing.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class PriceSnapshotTests
{
    // ── Construction & Immutability ──────────────────────────────────────────

    [Fact]
    public void Constructor_ValidArgs_SetsAllProperties()
    {
        // Arrange
        var marketProductId = Guid.NewGuid();
        var recordedBy = Guid.NewGuid();
        var before = DateTime.UtcNow;

        // Act
        var snapshot = new PriceSnapshot(marketProductId, 99.50m, 30, recordedBy);

        // Assert
        snapshot.Id.Should().NotBeEmpty();
        snapshot.MarketProductId.Should().Be(marketProductId);
        snapshot.Price.Should().Be(99.50m);
        snapshot.Quantity.Should().Be(30);
        snapshot.RecordedBy.Should().Be(recordedBy);
        snapshot.RecordedAt.Should().BeOnOrAfter(before);
        snapshot.RecordedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_NullRecordedBy_IsAllowed()
    {
        // Arrange
        var marketProductId = Guid.NewGuid();

        // Act
        var snapshot = new PriceSnapshot(marketProductId, 100m, 10, null);

        // Assert
        snapshot.RecordedBy.Should().BeNull();
    }

    [Fact]
    public void TwoSnapshotsCreatedSeparately_HaveDifferentIds()
    {
        // Arrange
        var marketProductId = Guid.NewGuid();

        // Act
        var s1 = new PriceSnapshot(marketProductId, 100m, 10, null);
        var s2 = new PriceSnapshot(marketProductId, 100m, 10, null);

        // Assert — append-only: each snapshot is a distinct record
        s1.Id.Should().NotBe(s2.Id);
    }

    [Fact]
    public void PriceSnapshot_DoesNotHaveSoftDeleteCapability()
    {
        // PriceSnapshot must NOT have DeletedAt / IsDeleted — it's append-only.
        // Compile-time check: verify no DeletedAt property exists on the type.
        var type = typeof(PriceSnapshot);
        type.GetProperty("DeletedAt").Should().BeNull(
            "PriceSnapshot is append-only and must not support soft delete.");
        type.GetProperty("IsDeleted").Should().BeNull(
            "PriceSnapshot is append-only and must not support soft delete.");
    }

    // ── PriceSnapshot.For factory (UC-PRI-06 DRY) ────────────────────────────

    private static readonly Guid MarketId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    [Fact]
    public void For_SetsMarketProductIdFromEntity()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100_000m, 200, ActorId);

        // Act
        var snapshot = PriceSnapshot.For(mp, ActorId);

        // Assert
        snapshot.MarketProductId.Should().Be(mp.Id);
    }

    [Fact]
    public void For_CapturesCurrentPriceAsPostChangeState()
    {
        // Arrange — simulate price having been updated before snapshot
        var mp = new MarketProduct(MarketId, ProductId, 100_000m, 200, ActorId);
        mp.UpdatePrice(135_000m, ActorId);

        // Act
        var snapshot = PriceSnapshot.For(mp, ActorId);

        // Assert — captures post-change CurrentPrice
        snapshot.Price.Should().Be(135_000m);
    }

    [Fact]
    public void For_CapturesCurrentQuantity()
    {
        // Arrange — simulate quantity having been updated before snapshot
        var mp = new MarketProduct(MarketId, ProductId, 100_000m, 200, ActorId);
        mp.UpdateAvailableQuantity(350, ActorId);

        // Act
        var snapshot = PriceSnapshot.For(mp, ActorId);

        // Assert
        snapshot.Quantity.Should().Be(350);
    }

    [Fact]
    public void For_SetsRecordedByFromActor()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100_000m, 200, null);
        var actor = Guid.NewGuid();

        // Act
        var snapshot = PriceSnapshot.For(mp, actor);

        // Assert
        snapshot.RecordedBy.Should().Be(actor);
    }

    [Fact]
    public void For_NullActor_SetsRecordedByNull()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100_000m, 200, ActorId);

        // Act
        var snapshot = PriceSnapshot.For(mp, actor: null);

        // Assert
        snapshot.RecordedBy.Should().BeNull();
    }

    [Fact]
    public void For_AssignsNonEmptyId()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100_000m, 200, ActorId);

        // Act
        var snapshot = PriceSnapshot.For(mp, ActorId);

        // Assert — PriceSnapshot constructor always assigns a new Guid
        snapshot.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void For_EachCallProducesDistinctId()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100_000m, 200, ActorId);

        // Act — two consecutive calls on the same entity produce independent rows
        var s1 = PriceSnapshot.For(mp, ActorId);
        var s2 = PriceSnapshot.For(mp, ActorId);

        // Assert
        s1.Id.Should().NotBe(s2.Id);
    }

    [Fact]
    public void For_SetsRecordedAtToApproximatelyNow()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100_000m, 200, ActorId);

        // Act
        var snapshot = PriceSnapshot.For(mp, ActorId);

        // Assert
        snapshot.RecordedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ── Append-only: no public setters ───────────────────────────────────────

    [Fact]
    public void PriceSnapshot_HasNoPublicSetters()
    {
        // Verify all properties are read-only (private init) — enforces immutability.
        var properties = typeof(PriceSnapshot).GetProperties();

        foreach (var prop in properties)
        {
            var publicSetter = prop.GetSetMethod(nonPublic: false);
            publicSetter.Should().BeNull(
                because: $"PriceSnapshot.{prop.Name} must be append-only (no public setter)");
        }
    }
}
