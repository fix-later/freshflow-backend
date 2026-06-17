using FluentAssertions;
using FreshFlow.Pricing.Domain.Entities;
using FreshFlow.Pricing.Domain.Events;

namespace FreshFlow.Pricing.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class MarketProductTests
{
    private static readonly Guid MarketId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    // ── Construction ────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ValidArgs_SetsAllProperties()
    {
        // Arrange & Act
        var mp = new MarketProduct(MarketId, ProductId, 100m, 50, ActorId);

        // Assert
        mp.MarketId.Should().Be(MarketId);
        mp.ProductId.Should().Be(ProductId);
        mp.CurrentPrice.Should().Be(100m);
        mp.CurrentQuantity.Should().Be(50);
        mp.ReservedQuantity.Should().Be(0);
        mp.UpdatedBy.Should().Be(ActorId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ZeroOrNegativePrice_ThrowsArgumentOutOfRangeException(decimal price)
    {
        // Act
        var act = () => new MarketProduct(MarketId, ProductId, price, 10, ActorId);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("price");
    }

    [Fact]
    public void Constructor_NegativeQuantity_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new MarketProduct(MarketId, ProductId, 100m, -1, ActorId);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("quantity");
    }

    // ── UpdatePrice ──────────────────────────────────────────────────────────

    [Fact]
    public void UpdatePrice_ValidNewPrice_UpdatesCurrentPrice()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 10, ActorId);

        // Act
        mp.UpdatePrice(150m, ActorId);

        // Assert
        mp.CurrentPrice.Should().Be(150m);
    }

    [Fact]
    public void UpdatePrice_ValidNewPrice_UpdatesUpdatedBy()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 10, null);
        var newActor = Guid.NewGuid();

        // Act
        mp.UpdatePrice(200m, newActor);

        // Assert
        mp.UpdatedBy.Should().Be(newActor);
    }

    [Fact]
    public void UpdatePrice_ValidNewPrice_RaisesPriceUpdatedDomainEvent()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 10, ActorId);

        // Act
        mp.UpdatePrice(150m, ActorId);

        // Assert
        mp.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PriceUpdatedDomainEvent>();
    }

    [Fact]
    public void UpdatePrice_ValidNewPrice_DomainEventContainsCorrectValues()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 10, ActorId);
        var newActor = Guid.NewGuid();

        // Act
        mp.UpdatePrice(150m, newActor);

        // Assert
        var evt = mp.DomainEvents.OfType<PriceUpdatedDomainEvent>().Single();
        evt.MarketProductId.Should().Be(mp.Id);
        evt.MarketId.Should().Be(MarketId);
        evt.ProductId.Should().Be(ProductId);
        evt.OldPrice.Should().Be(100m);
        evt.NewPrice.Should().Be(150m);
        evt.CurrentQuantity.Should().Be(10); // unchanged
        evt.UpdatedBy.Should().Be(newActor);
        evt.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdatePrice_SamePriceAsCurrent_StillRaisesDomainEvent()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 10, ActorId);

        // Act
        mp.UpdatePrice(100m, ActorId);

        // Assert — domain event still fires to record the re-confirmation
        mp.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PriceUpdatedDomainEvent>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdatePrice_ZeroOrNegativePrice_ThrowsArgumentOutOfRangeException(decimal price)
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 10, ActorId);

        // Act
        var act = () => mp.UpdatePrice(price, ActorId);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("price");
        mp.DomainEvents.Should().BeEmpty();
    }

    // ── ApplyUpdate ──────────────────────────────────────────────────────────

    [Fact]
    public void ApplyUpdate_PriceOnly_UpdatesPriceAndRaisesOneEvent()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 50, ActorId);

        // Act
        mp.ApplyUpdate(newPrice: 150m, newQuantity: null, actor: ActorId);

        // Assert
        mp.CurrentPrice.Should().Be(150m);
        mp.CurrentQuantity.Should().Be(50); // unchanged
        mp.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PriceUpdatedDomainEvent>();
    }

    [Fact]
    public void ApplyUpdate_QuantityOnly_UpdatesQuantityAndRaisesOneEvent()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 50, ActorId);

        // Act
        mp.ApplyUpdate(newPrice: null, newQuantity: 200, actor: ActorId);

        // Assert
        mp.CurrentQuantity.Should().Be(200);
        mp.CurrentPrice.Should().Be(100m); // unchanged
        mp.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PriceUpdatedDomainEvent>();
    }

    [Fact]
    public void ApplyUpdate_BothFields_UpdatesBothAndRaisesOneEvent()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 50, ActorId);

        // Act
        mp.ApplyUpdate(newPrice: 120m, newQuantity: 300, actor: ActorId);

        // Assert
        mp.CurrentPrice.Should().Be(120m);
        mp.CurrentQuantity.Should().Be(300);
        mp.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void ApplyUpdate_RaisedEvent_ContainsFullContext()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 50, null);
        var newActor = Guid.NewGuid();

        // Act
        mp.ApplyUpdate(newPrice: 130m, newQuantity: 75, actor: newActor);

        // Assert
        var evt = mp.DomainEvents.OfType<PriceUpdatedDomainEvent>().Single();
        evt.MarketProductId.Should().Be(mp.Id);
        evt.MarketId.Should().Be(MarketId);
        evt.ProductId.Should().Be(ProductId);
        evt.OldPrice.Should().Be(100m);
        evt.NewPrice.Should().Be(130m);
        evt.CurrentQuantity.Should().Be(75);
        evt.UpdatedBy.Should().Be(newActor);
        evt.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ApplyUpdate_QuantityOnlyChange_OldPriceEqualsNewPriceInEvent()
    {
        // Arrange — only quantity changes; price fields in event should be identical
        var mp = new MarketProduct(MarketId, ProductId, 100m, 50, ActorId);

        // Act
        mp.ApplyUpdate(newPrice: null, newQuantity: 200, actor: ActorId);

        // Assert — price fields should be unchanged (old == new)
        var evt = mp.DomainEvents.OfType<PriceUpdatedDomainEvent>().Single();
        evt.OldPrice.Should().Be(100m);
        evt.NewPrice.Should().Be(100m);
        evt.CurrentQuantity.Should().Be(200);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ApplyUpdate_ZeroOrNegativePrice_ThrowsArgumentOutOfRangeException(decimal price)
    {
        // Arrange — handler pre-validates, domain guards as defence-in-depth
        var mp = new MarketProduct(MarketId, ProductId, 100m, 10, ActorId);

        // Act
        var act = () => mp.ApplyUpdate(newPrice: price, newQuantity: null, actor: ActorId);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("price");
        mp.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ApplyUpdate_NegativeQuantity_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 10, ActorId);

        // Act
        var act = () => mp.ApplyUpdate(newPrice: null, newQuantity: -5, actor: ActorId);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("quantity");
        mp.DomainEvents.Should().BeEmpty();
    }

    // ── Fix #6: no-op guard ──────────────────────────────────────────────────

    [Fact]
    public void ApplyUpdate_BothParamsNull_RaisesNoDomainEvent()
    {
        // Arrange — calling with both null is a no-op; no synthetic event should be raised
        var mp = new MarketProduct(MarketId, ProductId, 100m, 50, ActorId);

        // Act
        mp.ApplyUpdate(newPrice: null, newQuantity: null, actor: ActorId);

        // Assert
        mp.DomainEvents.Should().BeEmpty(
            "a no-op ApplyUpdate must not synthesize a spurious PriceUpdatedDomainEvent");
    }

    [Fact]
    public void ApplyUpdate_BothParamsNull_DoesNotUpdateUpdatedAt()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 50, ActorId);
        var updatedAtBefore = mp.UpdatedAt;

        // Act
        mp.ApplyUpdate(newPrice: null, newQuantity: null, actor: ActorId);

        // Assert — UpdatedAt must remain unchanged for a no-op
        mp.UpdatedAt.Should().Be(updatedAtBefore);
    }

    [Fact]
    public void ApplyUpdate_BothParamsNull_DoesNotUpdateUpdatedBy()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 50, null); // UpdatedBy = null
        var newActor = Guid.NewGuid();

        // Act
        mp.ApplyUpdate(newPrice: null, newQuantity: null, actor: newActor);

        // Assert — UpdatedBy must NOT be changed for a no-op call
        mp.UpdatedBy.Should().BeNull();
    }

    // ── AvailableQuantity ────────────────────────────────────────────────────

    [Fact]
    public void AvailableQuantity_ReturnsCurrentQuantityMinusReserved()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 20, ActorId);

        // Act & Assert — ReservedQuantity defaults to 0 at construction
        mp.AvailableQuantity.Should().Be(20);
    }

    // ── IsOutOfStock ─────────────────────────────────────────────────────────

    [Fact]
    public void IsOutOfStock_WhenCurrentQuantityIsZero_ReturnsTrue()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 0, ActorId);

        // Assert
        mp.IsOutOfStock.Should().BeTrue();
    }

    [Fact]
    public void IsOutOfStock_WhenCurrentQuantityIsPositive_ReturnsFalse()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 5, ActorId);

        // Assert
        mp.IsOutOfStock.Should().BeFalse();
    }

    [Fact]
    public void IsOutOfStock_AfterUpdateAvailableQuantityToZero_ReturnsTrue()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 10, ActorId);

        // Act — UpdateQuantity removed; use UpdateAvailableQuantity (production path)
        mp.UpdateAvailableQuantity(0, ActorId);

        // Assert
        mp.IsOutOfStock.Should().BeTrue();
    }

    // ── UpdateAvailableQuantity ──────────────────────────────────────────────

    [Fact]
    public void UpdateAvailableQuantity_ValidQuantity_UpdatesCurrentQuantity()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 50, ActorId);

        // Act
        mp.UpdateAvailableQuantity(200, ActorId);

        // Assert
        mp.CurrentQuantity.Should().Be(200);
    }

    [Fact]
    public void UpdateAvailableQuantity_ZeroQuantity_SetsIsOutOfStock()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 50, ActorId);

        // Act
        mp.UpdateAvailableQuantity(0, ActorId);

        // Assert
        mp.CurrentQuantity.Should().Be(0);
        mp.IsOutOfStock.Should().BeTrue();
    }

    [Fact]
    public void UpdateAvailableQuantity_ValidQuantity_PriceUnchanged()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 50, ActorId);

        // Act
        mp.UpdateAvailableQuantity(300, ActorId);

        // Assert
        mp.CurrentPrice.Should().Be(100m);
    }

    [Fact]
    public void UpdateAvailableQuantity_RaisesPriceUpdatedDomainEvent()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 50, ActorId);

        // Act
        mp.UpdateAvailableQuantity(200, ActorId);

        // Assert — downstream handlers (SignalR, Redis) rely on this event
        mp.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<FreshFlow.Pricing.Domain.Events.PriceUpdatedDomainEvent>();
    }

    [Fact]
    public void UpdateAvailableQuantity_NegativeQuantity_ThrowsArgumentOutOfRangeException()
    {
        // Arrange — handler pre-validates; domain defends in depth
        var mp = new MarketProduct(MarketId, ProductId, 100m, 50, ActorId);

        // Act
        var act = () => mp.UpdateAvailableQuantity(-1, ActorId);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("quantity");
        mp.DomainEvents.Should().BeEmpty();
    }

    // ── DomainEvents ─────────────────────────────────────────────────────────

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        // Arrange
        var mp = new MarketProduct(MarketId, ProductId, 100m, 10, ActorId);
        mp.UpdatePrice(150m, ActorId);
        mp.UpdatePrice(200m, ActorId);
        mp.DomainEvents.Should().HaveCount(2);

        // Act
        mp.ClearDomainEvents();

        // Assert
        mp.DomainEvents.Should().BeEmpty();
    }
}
