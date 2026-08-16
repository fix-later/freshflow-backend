using FluentAssertions;
using FreshFlow.Hub.Application.Queries.GetHub;
using FreshFlow.Hub.Application.Queries.ListHubs;
using FreshFlow.Hub.UnitTests.TestDoubles;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class HubQueryHandlerTests
{
    [Fact]
    public async Task GetHub_ExistingHub_ReturnsDtoAsync()
    {
        var repository = new InMemoryHubRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await repository.AddAsync(hub, default);
        var sut = new GetHubQueryHandler(repository);

        var result = await sut.Handle(new GetHubQuery(hub.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.HubId.Should().Be(hub.Id);
        result.Value.AvailableCapacityKg.Should().Be(1000);
    }

    [Fact]
    public async Task GetHub_MissingHub_ReturnsNotFoundAsync()
    {
        var repository = new InMemoryHubRepository();
        var missingId = Guid.NewGuid();
        var sut = new GetHubQueryHandler(repository);

        var result = await sut.Handle(new GetHubQuery(missingId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_NOT_FOUND");
    }

    [Fact]
    public async Task ListHubs_ReturnsCapacityFieldsAsync()
    {
        var repository = new InMemoryHubRepository();
        var active = HubEntity.Create("Active Hub", null, null, null, 1000, null);
        var inactive = HubEntity.Create("Inactive Hub", null, null, null, 500, null);
        inactive.Deactivate();
        await repository.AddAsync(active, default);
        await repository.AddAsync(inactive, default);
        var sut = new ListHubsQueryHandler(repository);

        var result = await sut.Handle(new ListHubsQuery(PageSize: 10, IsActive: true), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items.Single().HubId.Should().Be(active.Id);
        result.Value.Items.Single().OccupiedCapacityKg.Should().Be(0);
        result.Value.Items.Single().AvailableCapacityKg.Should().Be(1000);
    }
}
