using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Application.Services;
using FreshFlow.Orders.Domain.Entities;

namespace FreshFlow.Orders.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class OrderPricingCalculatorTests
{
    [Fact]
    public void Calculate_AggregatesMoqAndCalculatesVatAndDistanceFee()
    {
        var marketProductId = Guid.NewGuid();
        var order = new Order(Guid.NewGuid(), null, null);
        order.AddItem(marketProductId, "Cà chua", 2, 10_000m);
        order.AddItem(marketProductId, "Cà chua", 3, 10_000m);
        var products = new Dictionary<Guid, MarketProductSnapshotDto>
        {
            [marketProductId] = new(
                marketProductId, "Cà chua", 10_000m, 100, 5, "8", 10m, 106m)
        };

        var result = OrderPricingCalculator.Calculate(
            order, products, 11.12m, new DeliveryFeePolicy(0m, 5_000m, 0m, 0m));

        result.IsSuccess.Should().BeTrue();
        result.Value.SubtotalAmount.Should().Be(50_000m);
        result.Value.VatAmount.Should().Be(4_000m);
        result.Value.DeliveryDistanceKm.Should().Be(11.12m);
        result.Value.DeliveryFee.Should().Be(55_600m);
        result.Value.TotalAmount.Should().Be(109_600m);
    }

    [Fact]
    public void Calculate_BelowMoq_ReturnsDeterministicError()
    {
        var marketProductId = Guid.NewGuid();
        var order = new Order(Guid.NewGuid(), null, null);
        order.AddItem(marketProductId, "Cà chua", 4, 10_000m);
        var products = new Dictionary<Guid, MarketProductSnapshotDto>
        {
            [marketProductId] = new(
                marketProductId, "Cà chua", 10_000m, 100, 5, "5", 10m, 106m)
        };

        var result = OrderPricingCalculator.Calculate(
            order, products, 0m, new DeliveryFeePolicy(0m, 5_000m, 0m, 0m));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MINIMUM_ORDER_QUANTITY_NOT_MET");
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void Calculate_ValidatesTotalQuantityAcrossProductLines(
        int secondLineQuantity, bool expectedSuccess)
    {
        var marketProductId = Guid.NewGuid();
        var order = new Order(Guid.NewGuid(), null, null);
        order.AddItem(marketProductId, "Cà chua", 2, 10_000m);
        order.AddItem(marketProductId, "Cà chua", secondLineQuantity, 10_000m);
        var products = new Dictionary<Guid, MarketProductSnapshotDto>
        {
            [marketProductId] = new(
                marketProductId, "Cà chua", 10_000m, 100,
                PackingCode: "BOX-5", PackingWeightKg: 5m)
        };

        var result = OrderPricingCalculator.Calculate(
            order, products, 0m, new DeliveryFeePolicy(0m, 0m, 0m, 0m));

        result.IsSuccess.Should().Be(expectedSuccess);
        if (!expectedSuccess)
            result.Error.Code.Should().Be("PACKING_QUANTITY_MISMATCH");
    }

    [Fact]
    public void Calculate_AppliesBaseMinimumAndRoundingUnit()
    {
        var marketProductId = Guid.NewGuid();
        var order = new Order(Guid.NewGuid(), null, null);
        order.AddItem(marketProductId, "Cà chua", 1, 10_000m);
        var products = new Dictionary<Guid, MarketProductSnapshotDto>
        {
            [marketProductId] = new(marketProductId, "Cà chua", 10_000m, 100)
        };

        var result = OrderPricingCalculator.Calculate(
            order, products, 1.2m, new DeliveryFeePolicy(1_000m, 2_000m, 4_100m, 1_000m));

        result.IsSuccess.Should().BeTrue();
        result.Value.DeliveryFee.Should().Be(4_000m);
    }
}
