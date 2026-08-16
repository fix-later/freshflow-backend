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
    private static readonly DateOnly ServiceDate = new(2026, 7, 29);

    [Fact]
    public async Task Handle_NoExistingLine_InsertsSortedRowAsync()
    {
        var hubs = new InMemoryHubRepository();
        var orders = new InMemoryOrderLookupReader();
        var hubOrders = new InMemoryHubRestaurantOrderReader();
        var progress = new InMemoryHubSortingProgressRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await hubs.AddAsync(hub, default);
        var orderItemId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        orders.Add(orderItemId, orderId, Guid.NewGuid(), quantity: 4m);
        hubOrders.Add(hub.Id, ServiceDate, orderId);
        var sut = new MarkLineSortedCommandHandler(hubs, progress, orders, hubOrders);

        var result = await sut.Handle(
            new MarkLineSortedCommand(hub.Id, ServiceDate, orderItemId, 4m, userId),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(HubSortingProgress.StatusSorted);
        progress.Lines.Should().ContainSingle();
        progress.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ExistingLineForSameHubDateAndItem_UpdatesInPlaceInsteadOfInsertingAsync()
    {
        var hubs = new InMemoryHubRepository();
        var orders = new InMemoryOrderLookupReader();
        var progress = new InMemoryHubSortingProgressRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var hubOrders = new InMemoryHubRestaurantOrderReader();
        await hubs.AddAsync(hub, default);
        var orderItemId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        orders.Add(orderItemId, orderId, Guid.NewGuid(), quantity: 9m);
        hubOrders.Add(hub.Id, ServiceDate, orderId);
        var sut = new MarkLineSortedCommandHandler(hubs, progress, orders, hubOrders);
        var first = await sut.Handle(
            new MarkLineSortedCommand(hub.Id, ServiceDate, orderItemId, 4m, Guid.NewGuid()), default);
        first.Value.Status.Should().Be(HubSortingProgress.StatusPending);

        var result = await sut.Handle(
            new MarkLineSortedCommand(hub.Id, ServiceDate, orderItemId, 9m, Guid.NewGuid()),
            default);

        result.IsSuccess.Should().BeTrue();
        progress.Lines.Should().ContainSingle().Which.SortedQuantityKg.Should().Be(9m);
        result.Value.Status.Should().Be(HubSortingProgress.StatusSorted);
        result.Value.RequiredQuantityKg.Should().Be(9m);
        result.Value.RemainingQuantityKg.Should().Be(0m);
        progress.SaveChangesCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_OrderOutsideHub_ReturnsValidationErrorAsync()
    {
        var hubs = new InMemoryHubRepository();
        var orders = new InMemoryOrderLookupReader();
        var progress = new InMemoryHubSortingProgressRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await hubs.AddAsync(hub, default);
        var orderItemId = Guid.NewGuid();
        orders.Add(orderItemId, Guid.NewGuid(), Guid.NewGuid(), quantity: 4m);
        var sut = new MarkLineSortedCommandHandler(
            hubs, progress, orders, new InMemoryHubRestaurantOrderReader());

        var result = await sut.Handle(
            new MarkLineSortedCommand(hub.Id, ServiceDate, orderItemId, 4m, Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_NOT_AT_HUB");
        progress.Lines.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_HubMissing_ReturnsNotFoundAsync()
    {
        var sut = new MarkLineSortedCommandHandler(
            new InMemoryHubRepository(),
            new InMemoryHubSortingProgressRepository(),
            new InMemoryOrderLookupReader(),
            new InMemoryHubRestaurantOrderReader());

        var result = await sut.Handle(
            new MarkLineSortedCommand(Guid.NewGuid(), ServiceDate, Guid.NewGuid(), 1m, Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_NOT_FOUND");
    }

    [Fact]
    public async Task GetSortingProgress_ReturnsOnlyLinesForRequestedHubAndDateAsync()
    {
        var hubs = new InMemoryHubRepository();
        var orders = new InMemoryOrderLookupReader();
        var progress = new InMemoryHubSortingProgressRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await hubs.AddAsync(hub, default);
        var line = HubSortingProgress.Create(hub.Id, ServiceDate, Guid.NewGuid());
        orders.Add(line.OrderItemId, Guid.NewGuid(), Guid.NewGuid(), quantity: 2m);
        line.UpdateSortedQuantity(2m, 2m, Guid.NewGuid(), DateTime.UtcNow);
        await progress.AddAsync(line, default);
        await progress.AddAsync(
            HubSortingProgress.Create(hub.Id, ServiceDate.AddDays(1), Guid.NewGuid()), default);
        var sut = new GetSortingProgressQueryHandler(hubs, progress, orders);

        var result = await sut.Handle(new GetSortingProgressQuery(hub.Id, ServiceDate), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.OrderItemId.Should().Be(line.OrderItemId);
    }
}
