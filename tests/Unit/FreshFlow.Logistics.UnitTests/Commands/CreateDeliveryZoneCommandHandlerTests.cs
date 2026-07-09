using FluentAssertions;
using FreshFlow.Logistics.Application.Commands.DeliveryZones.Create;
using FreshFlow.Logistics.UnitTests.TestDoubles;

namespace FreshFlow.Logistics.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class CreateDeliveryZoneCommandHandlerTests
{
    [Fact]
    public async Task Handle_NewZone_CreatesActiveZoneAsync()
    {
        var repository = new InMemoryDeliveryZoneRepository();
        var sut = new CreateDeliveryZoneCommandHandler(repository);

        var result = await sut.Handle(
            new CreateDeliveryZoneCommand("district_1", "District 1", "Central"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("DISTRICT_1");
        result.Value.Name.Should().Be("District 1");
        result.Value.Description.Should().Be("Central");
        result.Value.IsActive.Should().BeTrue();
        repository.Zones.Should().ContainSingle();
        repository.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DuplicateCodeIgnoringCase_ReturnsConflictAndDoesNotCreateRecordAsync()
    {
        var repository = new InMemoryDeliveryZoneRepository();
        await repository.AddAsync(new("district_1", "District 1", null), default);
        var sut = new CreateDeliveryZoneCommandHandler(repository);

        var result = await sut.Handle(
            new CreateDeliveryZoneCommand("DISTRICT_1", "Duplicate", null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DELIVERY_ZONE_CODE_EXISTS");
        repository.Zones.Should().ContainSingle();
        repository.SaveChangesCount.Should().Be(0);
    }
}
