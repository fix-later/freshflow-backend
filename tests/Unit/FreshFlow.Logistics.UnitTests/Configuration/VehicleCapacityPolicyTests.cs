using FluentAssertions;
using FreshFlow.Logistics.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace FreshFlow.Logistics.UnitTests.Configuration;

[Trait("Category", "Unit")]
public sealed class VehicleCapacityPolicyTests
{
    [Theory]
    [InlineData("5", 5)]
    [InlineData("not-a-number", 20)]
    [InlineData("0", 20)]
    [InlineData("-1", 20)]
    [InlineData(null, 20)]
    public void Constructor_ReadsMaxStopsPerVehicleWithFallback(string? configuredValue, int expected)
    {
        var config = configuredValue is null
            ? new ConfigurationBuilder().Build()
            : new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Logistics:MaxStopsPerVehicle"] = configuredValue
                })
                .Build();

        var sut = new VehicleCapacityPolicy(config);

        sut.MaxStopsPerVehicle.Should().Be(expected);
    }

    [Theory]
    [InlineData("2.5", 2.5)]
    [InlineData("not-a-number", 2)]
    [InlineData("0", 2)]
    [InlineData("-1", 2)]
    [InlineData(null, 2)]
    public void Constructor_ReadsBoxTareKgWithInvariantFallback(
        string? configuredValue, decimal expected)
    {
        var config = configuredValue is null
            ? new ConfigurationBuilder().Build()
            : new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Logistics:Box:TareKg"] = configuredValue
                })
                .Build();

        var sut = new VehicleCapacityPolicy(config);

        sut.BoxTareKg.Should().Be(expected);
    }
}
