using FluentAssertions;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Commands.UpdateHub;
using FreshFlow.Hub.UnitTests.TestDoubles;
using NSubstitute;
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
        var sut = new UpdateHubCommandHandler(repository, Substitute.For<IMarketReader>());

        var result = await sut.Handle(
            new UpdateHubCommand(hub.Id, "Updated Hub", "456 Road", 11m, 107m, 1500, updatedManagedBy, null),
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
        var sut = new UpdateHubCommandHandler(repository, Substitute.For<IMarketReader>());

        var result = await sut.Handle(
            new UpdateHubCommand(missingId, "Updated Hub", null, null, null, 1500, null, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_NOT_FOUND");
        repository.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CapacityBelowOccupied_ReturnsValidationErrorAsync()
    {
        var repository = new InMemoryHubRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        hub.ApplyInbound(250m);
        await repository.AddAsync(hub, default);
        var sut = new UpdateHubCommandHandler(repository, Substitute.For<IMarketReader>());

        var result = await sut.Handle(
            new UpdateHubCommand(hub.Id, "Main Hub", null, null, null, 100m, null, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_CAPACITY_BELOW_OCCUPIED");
        repository.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_AssignNewActiveMarket_UpdatesHubMarketIdAsync()
    {
        var repository = new InMemoryHubRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await repository.AddAsync(hub, default);
        var marketId = Guid.NewGuid();
        var markets = Substitute.For<IMarketReader>();
        markets.FindAsync(marketId, default).Returns(new MarketSnapshot(marketId, true));
        var sut = new UpdateHubCommandHandler(repository, markets);

        var result = await sut.Handle(
            new UpdateHubCommand(hub.Id, "Main Hub", null, null, null, 1000, null, marketId),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.MarketId.Should().Be(marketId);
        repository.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_MarketNotFound_ReturnsNotFoundAsync()
    {
        var repository = new InMemoryHubRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await repository.AddAsync(hub, default);
        var marketId = Guid.NewGuid();
        var markets = Substitute.For<IMarketReader>();
        markets.FindAsync(marketId, default).Returns((MarketSnapshot?)null);
        var sut = new UpdateHubCommandHandler(repository, markets);

        var result = await sut.Handle(
            new UpdateHubCommand(hub.Id, "Main Hub", null, null, null, 1000, null, marketId),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MARKET_NOT_FOUND");
        repository.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MarketInactive_ReturnsValidationErrorAsync()
    {
        var repository = new InMemoryHubRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await repository.AddAsync(hub, default);
        var marketId = Guid.NewGuid();
        var markets = Substitute.For<IMarketReader>();
        markets.FindAsync(marketId, default).Returns(new MarketSnapshot(marketId, false));
        var sut = new UpdateHubCommandHandler(repository, markets);

        var result = await sut.Handle(
            new UpdateHubCommand(hub.Id, "Main Hub", null, null, null, 1000, null, marketId),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MARKET_INACTIVE");
        repository.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MarketAlreadyOwnedByAnotherActiveHub_ReturnsConflictAsync()
    {
        var repository = new InMemoryHubRepository();
        var marketId = Guid.NewGuid();
        var otherHub = HubEntity.Create("Other Hub", null, null, null, 500, null, marketId);
        await repository.AddAsync(otherHub, default);
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await repository.AddAsync(hub, default);
        var markets = Substitute.For<IMarketReader>();
        markets.FindAsync(marketId, default).Returns(new MarketSnapshot(marketId, true));
        var sut = new UpdateHubCommandHandler(repository, markets);

        var result = await sut.Handle(
            new UpdateHubCommand(hub.Id, "Main Hub", null, null, null, 1000, null, marketId),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_ALREADY_CONFIGURED_FOR_MARKET");
        repository.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MarketIdUnchanged_DoesNotLookUpMarketAsync()
    {
        var repository = new InMemoryHubRepository();
        var marketId = Guid.NewGuid();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null, marketId);
        await repository.AddAsync(hub, default);
        var markets = Substitute.For<IMarketReader>();
        var sut = new UpdateHubCommandHandler(repository, markets);

        var result = await sut.Handle(
            new UpdateHubCommand(hub.Id, "Main Hub", null, null, null, 1000, null, marketId),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.MarketId.Should().Be(marketId);
        await markets.DidNotReceive().FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        repository.SaveChangesCount.Should().Be(1);
    }
}
