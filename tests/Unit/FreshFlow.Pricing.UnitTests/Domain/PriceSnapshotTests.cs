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
}
