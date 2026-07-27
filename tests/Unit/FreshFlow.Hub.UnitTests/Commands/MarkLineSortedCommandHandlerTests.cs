using FluentAssertions;
using FreshFlow.Hub.Application.Commands.MarkLineSorted;
using FreshFlow.Hub.Application.Queries.GetSortingProgress;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Hub.UnitTests.TestDoubles;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class MarkLineSortedCommandHandlerTests
{
    [Fact]
    public async Task Handle_NoExistingLine_InsertsSortedRowAsync()
    {
        var hubs = new InMemoryHubRepository();
        var progress = new InMemoryHubSortingProgressRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await hubs.AddAsync(hub, default);
        var routeId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sut = new MarkLineSortedCommandHandler(hubs, progress);

        var result = await sut.Handle(
            new MarkLineSortedCommand(hub.Id, routeId, orderItemId, 4m, userId),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(HubSortingProgress.StatusSorted);
        progress.Lines.Should().ContainSingle();
        progress.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ExistingLineForSameRouteAndItem_UpdatesInPlaceInsteadOfInsertingAsync()
    {
        var hubs = new InMemoryHubRepository();
        var progress = new InMemoryHubSortingProgressRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await hubs.AddAsync(hub, default);
        var routeId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var sut = new MarkLineSortedCommandHandler(hubs, progress);
        await sut.Handle(new MarkLineSortedCommand(hub.Id, routeId, orderItemId, 4m, Guid.NewGuid()), default);

        var result = await sut.Handle(
            new MarkLineSortedCommand(hub.Id, routeId, orderItemId, 9m, Guid.NewGuid()),
            default);

        result.IsSuccess.Should().BeTrue();
        progress.Lines.Should().ContainSingle().Which.SortedQuantityKg.Should().Be(9m);
        progress.SaveChangesCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_HubMissing_ReturnsNotFoundAsync()
    {
        var sut = new MarkLineSortedCommandHandler(new InMemoryHubRepository(), new InMemoryHubSortingProgressRepository());

        var result = await sut.Handle(
            new MarkLineSortedCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1m, Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_NOT_FOUND");
    }

    [Fact]
    public async Task GetSortingProgress_ReturnsOnlyLinesForRequestedRouteAsync()
    {
        var hubs = new InMemoryHubRepository();
        var progress = new InMemoryHubSortingProgressRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await hubs.AddAsync(hub, default);
        var routeId = Guid.NewGuid();
        var line = HubSortingProgress.Create(routeId, Guid.NewGuid());
        line.MarkSorted(2m, Guid.NewGuid(), DateTime.UtcNow);
        await progress.AddAsync(line, default);
        await progress.AddAsync(HubSortingProgress.Create(Guid.NewGuid(), Guid.NewGuid()), default);
        var sut = new GetSortingProgressQueryHandler(hubs, progress);

        var result = await sut.Handle(new GetSortingProgressQuery(hub.Id, routeId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.OrderItemId.Should().Be(line.OrderItemId);
    }
}
