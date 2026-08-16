using FluentAssertions;
using FreshFlow.Orders.Domain.Entities;

namespace FreshFlow.Orders.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class OrderItemTests
{
    private static readonly Guid MarketProductId = Guid.NewGuid();

    [Fact]
    public void Constructor_ValidArgs_SetsAllProperties()
    {
        // Act
        var item = new OrderItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);

        // Assert
        item.MarketProductId.Should().Be(MarketProductId);
        item.ProductNameSnapshot.Should().Be("Cà chua");
        item.Quantity.Should().Be(5);
        item.UnitPrice.Should().Be(20_000m);
        item.LockedUnitPrice.Should().BeNull();
        item.LockedTotal.Should().BeNull();
        item.ActualQuantity.Should().BeNull();
    }

    [Fact]
    public void Subtotal_ReturnsQuantityTimesUnitPrice()
    {
        // Arrange
        var item = new OrderItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);

        // Act & Assert
        item.Subtotal.Should().Be(100_000m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ZeroOrNegativeQuantity_ThrowsArgumentOutOfRangeException(int quantity)
    {
        // Act
        var act = () => new OrderItem(MarketProductId, "Cà chua", quantity, unitPrice: 20_000m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("quantity");
    }

    [Fact]
    public void Constructor_NegativeUnitPrice_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var act = () => new OrderItem(MarketProductId, "Cà chua", quantity: 1, unitPrice: -1m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("unitPrice");
    }

    [Fact]
    public void Constructor_EmptyProductNameSnapshot_ThrowsArgumentException()
    {
        // Act
        var act = () => new OrderItem(MarketProductId, "", quantity: 1, unitPrice: 1m);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("productNameSnapshot");
    }

    // ── UpdateQuantity ───────────────────────────────────────────────────────

    [Fact]
    public void UpdateQuantity_ValidQuantity_UpdatesQuantity()
    {
        // Arrange
        var item = new OrderItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);

        // Act
        item.UpdateQuantity(10);

        // Assert
        item.Quantity.Should().Be(10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateQuantity_ZeroOrNegativeQuantity_ThrowsArgumentOutOfRangeException(int quantity)
    {
        // Arrange
        var item = new OrderItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);

        // Act
        var act = () => item.UpdateQuantity(quantity);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("quantity");
    }

    // ── LockPrice ────────────────────────────────────────────────────────────

    [Fact]
    public void LockPrice_ValidArgs_SetsLockedUnitPriceAndLockedTotal()
    {
        // Arrange
        var item = new OrderItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);

        // Act
        item.LockPrice(22_000m);

        // Assert
        item.LockedUnitPrice.Should().Be(22_000m);
        item.LockedTotal.Should().Be(110_000m);
    }

    [Fact]
    public void LockPrice_NegativePrice_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var item = new OrderItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);

        // Act
        var act = () => item.LockPrice(-1m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("lockedUnitPrice");
    }

    // ── RecordActualQuantity ─────────────────────────────────────────────────

    [Fact]
    public void RecordActualQuantity_ValidArgs_SetsActualQuantity()
    {
        // Arrange
        var item = new OrderItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);

        // Act
        item.RecordActualQuantity(4.5m);

        // Assert
        item.ActualQuantity.Should().Be(4.5m);
    }

    [Fact]
    public void RecordActualQuantity_NegativeQuantity_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var item = new OrderItem(MarketProductId, "Cà chua", quantity: 5, unitPrice: 20_000m);

        // Act
        var act = () => item.RecordActualQuantity(-1m);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("actualQuantity");
    }
}
