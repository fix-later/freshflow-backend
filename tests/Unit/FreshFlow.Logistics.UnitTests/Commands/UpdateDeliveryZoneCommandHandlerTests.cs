using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.DeliveryZones.Update;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.UnitTests.TestDoubles;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateDeliveryZoneCommandHandlerTests
{
    [Fact]
    public async Task Handle_ExistingZone_UpdatesNameAndDescriptionAsync()
    {
        var repository = new InMemoryDeliveryZoneRepository();
        var zone = new DeliveryZone("DISTRICT_1", "District 1", "Old");
        await repository.AddAsync(zone, default);
        var sut = new UpdateDeliveryZoneCommandHandler(repository);

        var result = await sut.Handle(
            new UpdateDeliveryZoneCommand(zone.Id, "District One", "New"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("DISTRICT_1");
        result.Value.Name.Should().Be("District One");
        result.Value.Description.Should().Be("New");
        repository.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_MissingZone_ReturnsNotFoundAsync()
    {
        var repository = new InMemoryDeliveryZoneRepository();
        var sut = new UpdateDeliveryZoneCommandHandler(repository);
        var id = Guid.NewGuid();

        var result = await sut.Handle(new UpdateDeliveryZoneCommand(id, "District 1", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ZONE_NOT_FOUND");
        repository.SaveChangesCount.Should().Be(0);
    }
}
