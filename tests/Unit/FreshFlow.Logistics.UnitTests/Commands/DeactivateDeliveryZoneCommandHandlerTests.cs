using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.DeliveryZones.Deactivate;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.UnitTests.TestDoubles;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class DeactivateDeliveryZoneCommandHandlerTests
{
    [Fact]
    public async Task Handle_ExistingZone_DeactivatesZoneAsync()
    {
        var repository = new InMemoryDeliveryZoneRepository();
        var zone = new DeliveryZone("DISTRICT_1", "District 1", null);
        await repository.AddAsync(zone, default);
        var sut = new DeactivateDeliveryZoneCommandHandler(repository);

        var result = await sut.Handle(new DeactivateDeliveryZoneCommand(zone.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
        zone.DeletedAt.Should().NotBeNull();
        repository.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AlreadyDeactivatedZone_ReturnsSuccessAndKeepsDeletedAtAsync()
    {
        var repository = new InMemoryDeliveryZoneRepository();
        var zone = new DeliveryZone("DISTRICT_1", "District 1", null);
        await repository.AddAsync(zone, default);
        var sut = new DeactivateDeliveryZoneCommandHandler(repository);

        var first = await sut.Handle(new DeactivateDeliveryZoneCommand(zone.Id), default);
        var deletedAt = zone.DeletedAt;
        var second = await sut.Handle(new DeactivateDeliveryZoneCommand(zone.Id), default);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        zone.DeletedAt.Should().Be(deletedAt);
        repository.SaveChangesCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_MissingZone_ReturnsNotFoundAsync()
    {
        var repository = new InMemoryDeliveryZoneRepository();
        var sut = new DeactivateDeliveryZoneCommandHandler(repository);
        var id = Guid.NewGuid();

        var result = await sut.Handle(new DeactivateDeliveryZoneCommand(id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ZONE_NOT_FOUND");
        repository.SaveChangesCount.Should().Be(0);
    }
}
