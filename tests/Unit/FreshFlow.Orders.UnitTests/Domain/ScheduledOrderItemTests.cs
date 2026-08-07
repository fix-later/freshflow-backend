using FluentAssertions;
using FreshFlow.Orders.Domain.Entities;

namespace FreshFlow.Orders.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class ScheduledOrderItemTests
{
    [Fact]
    public void Constructor_ValidArgs_SetsProperties()
    {
        var marketProductId = Guid.NewGuid();

        var item = new ScheduledOrderItem(marketProductId, 4);

        item.MarketProductId.Should().Be(marketProductId);
        item.Quantity.Should().Be(4);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveQuantity_Throws(int quantity)
    {
        var act = () => new ScheduledOrderItem(Guid.NewGuid(), quantity);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpdateQuantity_ValidValue_UpdatesQuantity()
    {
        var item = new ScheduledOrderItem(Guid.NewGuid(), 1);

        item.UpdateQuantity(9);

        item.Quantity.Should().Be(9);
    }

    [Fact]
    public void UpdateQuantity_NonPositiveValue_Throws()
    {
        var item = new ScheduledOrderItem(Guid.NewGuid(), 1);

        var act = () => item.UpdateQuantity(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
