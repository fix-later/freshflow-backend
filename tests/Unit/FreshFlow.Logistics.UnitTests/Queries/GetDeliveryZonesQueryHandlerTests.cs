using FluentAssertions;
using FreshFlow.Logistics.Application.Queries.DeliveryZones.GetDeliveryZones;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.UnitTests.TestDoubles;

namespace FreshFlow.Logistics.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetDeliveryZonesQueryHandlerTests
{
    [Fact]
    public void Query_DefaultActiveOnly_IsTrue()
    {
        var query = new GetDeliveryZonesQuery();

        query.ActiveOnly.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ActiveOnlyTrue_ReturnsOnlyActiveZonesAsync()
    {
        var repository = new InMemoryDeliveryZoneRepository();
        var active = new DeliveryZone("DISTRICT_1", "District 1", null);
        var inactive = new DeliveryZone("DISTRICT_2", "District 2", null);
        inactive.Deactivate();
        await repository.AddAsync(active, default);
        await repository.AddAsync(inactive, default);
        var sut = new GetDeliveryZonesQueryHandler(repository);

        var result = await sut.Handle(new GetDeliveryZonesQuery(ActiveOnly: true), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(z => z.Id == active.Id);
        result.Value.Should().OnlyContain(z => z.IsActive);
    }

    [Fact]
    public async Task Handle_ActiveOnlyFalse_ReturnsAllZonesAsync()
    {
        var repository = new InMemoryDeliveryZoneRepository();
        var active = new DeliveryZone("DISTRICT_1", "District 1", null);
        var inactive = new DeliveryZone("DISTRICT_2", "District 2", null);
        inactive.Deactivate();
        await repository.AddAsync(active, default);
        await repository.AddAsync(inactive, default);
        var sut = new GetDeliveryZonesQueryHandler(repository);

        var result = await sut.Handle(new GetDeliveryZonesQuery(ActiveOnly: false), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(z => z.IsActive);
        result.Value.Should().Contain(z => !z.IsActive);
    }
}
