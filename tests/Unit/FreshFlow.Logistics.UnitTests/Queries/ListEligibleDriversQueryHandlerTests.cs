using FluentAssertions;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Queries.ListEligibleDrivers;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class ListEligibleDriversQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsWhateverTheReaderProvides_PassthroughAsync()
    {
        var hubId = Guid.NewGuid();
        var driver = new DriverDto(
            Guid.NewGuid(), "Driver One", "driver@test.freshflow", "driver", true);
        var drivers = Substitute.For<IDriverReader>();
        drivers.ListEligibleAsync(hubId, Arg.Any<CancellationToken>()).Returns([driver]);

        var sut = new ListEligibleDriversQueryHandler(drivers);
        var result = await sut.Handle(new ListEligibleDriversQuery(hubId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().Be(driver);
    }
}
