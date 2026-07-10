using FluentAssertions;
using FreshFlow.Hub.Application.Commands.UpdateHub;
using FreshFlow.Hub.UnitTests.TestDoubles;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UpdateHubCommandHandlerTests
{
    [Fact]
    public async Task Handle_ExistingHub_UpdatesEditableFieldsAsync()
    {
        var repository = new InMemoryHubRepository();
        var managedBy = Guid.NewGuid();
        var hub = HubEntity.Create("Main Hub", "123 Road", 10m, 106m, 1000, managedBy);
        var updatedManagedBy = Guid.NewGuid();
        await repository.AddAsync(hub, default);
        var sut = new UpdateHubCommandHandler(repository);

        var result = await sut.Handle(
            new UpdateHubCommand(hub.Id, "Updated Hub", "456 Road", 11m, 107m, 1500, updatedManagedBy),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Updated Hub");
        result.Value.Address.Should().Be("456 Road");
        result.Value.Latitude.Should().Be(11m);
        result.Value.Longitude.Should().Be(107m);
        result.Value.CapacityKg.Should().Be(1500);
        result.Value.ManagedBy.Should().Be(updatedManagedBy);
        repository.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_MissingHub_ReturnsNotFoundAsync()
    {
        var repository = new InMemoryHubRepository();
        var missingId = Guid.NewGuid();
        var sut = new UpdateHubCommandHandler(repository);

        var result = await sut.Handle(
            new UpdateHubCommand(missingId, "Updated Hub", null, null, null, 1500, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_NOT_FOUND");
        repository.SaveChangesCount.Should().Be(0);
    }
}
