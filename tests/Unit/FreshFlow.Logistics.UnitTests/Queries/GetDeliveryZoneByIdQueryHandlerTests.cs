using FluentAssertions;
using FreshFlow.Logistics.Application.Queries.DeliveryZones.GetDeliveryZoneById;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.UnitTests.TestDoubles;

namespace FreshFlow.Logistics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetDeliveryZoneByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExistingZone_ReturnsDtoAsync()
    {
        var repository = new InMemoryDeliveryZoneRepository();
        var zone = new DeliveryZone("DISTRICT_1", "District 1", null);
        await repository.AddAsync(zone, default);
        var sut = new GetDeliveryZoneByIdQueryHandler(repository);

        var result = await sut.Handle(new GetDeliveryZoneByIdQuery(zone.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(zone.Id);
        result.Value.Code.Should().Be("DISTRICT_1");
    }

    [Fact]
    public async Task Handle_MissingZone_ReturnsNotFoundAsync()
    {
        var repository = new InMemoryDeliveryZoneRepository();
        var sut = new GetDeliveryZoneByIdQueryHandler(repository);
        var id = Guid.NewGuid();

        var result = await sut.Handle(new GetDeliveryZoneByIdQuery(id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ZONE_NOT_FOUND");
    }
}
