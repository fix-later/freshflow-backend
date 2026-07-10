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
}
