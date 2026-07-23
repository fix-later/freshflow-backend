using FluentAssertions;
using FreshFlow.Logistics.Application.Common;
using FreshFlow.Logistics.Application.Dtos;

namespace FreshFlow.Logistics.UnitTests.Common;

[Trait("Category", "Unit")]
public sealed class ShipmentEstimatorTests
{
    private const decimal TareKg = 2m;

    [Fact]
    public void Estimate_ExactThreeBoxes_IncludesTareInLoad()
    {
        var result = Estimate([new OrderPackingLine("Cá", 45, 15m)]);

        result.TotalBoxes.Should().Be(3);
        result.TotalLoadKg.Should().Be(51m);
        result.Lines.Single().Should().Be(
            new ShipmentLineDto("Cá", 45, 15m, 3, 51m));
    }

    [Fact]
    public void Estimate_PartialBox_RoundsUp()
    {
        var result = Estimate([new OrderPackingLine("Rau", 25, 15m)]);

        result.TotalBoxes.Should().Be(2);
        result.TotalLoadKg.Should().Be(34m);
    }

    [Fact]
    public void Estimate_MultipleLines_SumsBoxesAndLoad()
    {
        var result = Estimate(TwoPackedLines());

        result.TotalBoxes.Should().Be(5);
        result.TotalLoadKg.Should().Be(85m);
    }

    [Fact]
    public void Estimate_MissingPackingCode_ListsProductAndExcludesTotals()
    {
        var result = Estimate([new OrderPackingLine("Muối", 10, null)]);

        result.TotalBoxes.Should().Be(0);
        result.TotalLoadKg.Should().Be(0m);
        result.Lines.Should().BeEmpty();
        result.MissingPackingCode.Should().Equal("Muối");
    }

    [Fact]
    public void Estimate_LoadWithinVehicleCapacity_Fits()
    {
        var result = Estimate(TwoPackedLines(), vehicleCapacityKg: 100m);

        result.TotalLoadKg.Should().Be(85m);
        result.FitsVehicle.Should().BeTrue();
    }

    [Fact]
    public void Estimate_LoadAboveVehicleCapacity_DoesNotFit()
    {
        var result = Estimate(TwoPackedLines(), vehicleCapacityKg: 80m);

        result.TotalLoadKg.Should().Be(85m);
        result.FitsVehicle.Should().BeFalse();
    }

    [Fact]
    public void Estimate_NoVehicleCapacity_ReturnsNullFit()
    {
        var result = Estimate(TwoPackedLines());

        result.TotalLoadKg.Should().Be(85m);
        result.FitsVehicle.Should().BeNull();
    }

    private static IReadOnlyList<OrderPackingLine> TwoPackedLines() =>
        [new("Cá", 45, 15m), new("Rau", 25, 15m)];

    private static ShipmentEstimateDto Estimate(
        IReadOnlyList<OrderPackingLine> lines,
        decimal? vehicleCapacityKg = null) =>
        ShipmentEstimator.Estimate(
            Guid.NewGuid(), lines, TareKg,
            vehicleCapacityKg.HasValue ? Guid.NewGuid() : null,
            vehicleCapacityKg);
}
