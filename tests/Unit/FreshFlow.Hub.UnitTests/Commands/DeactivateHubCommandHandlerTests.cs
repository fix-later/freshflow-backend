using FluentAssertions;
using FreshFlow.Hub.Application.Commands.DeactivateHub;
using FreshFlow.Hub.UnitTests.TestDoubles;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class DeactivateHubCommandHandlerTests
{
    [Fact]
    public async Task Handle_ExistingHubWithoutPendingInbound_DeactivatesAsync()
    {
        var repository = new InMemoryHubRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await repository.AddAsync(hub, default);
        var sut = new DeactivateHubCommandHandler(repository);

        var result = await sut.Handle(new DeactivateHubCommand(hub.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
        hub.IsActive.Should().BeFalse();
        repository.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ExistingHubWithPendingInbound_ReturnsConflictHookAsync()
    {
        var repository = new InMemoryHubRepository { HasPendingInboundResult = true };
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await repository.AddAsync(hub, default);
        var sut = new DeactivateHubCommandHandler(repository);

        var result = await sut.Handle(new DeactivateHubCommand(hub.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_HAS_PENDING_DELIVERIES");
        hub.IsActive.Should().BeTrue();
        repository.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MissingHub_ReturnsNotFoundAsync()
    {
        var repository = new InMemoryHubRepository();
        var missingId = Guid.NewGuid();
        var sut = new DeactivateHubCommandHandler(repository);

        var result = await sut.Handle(new DeactivateHubCommand(missingId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_NOT_FOUND");
    }
}
