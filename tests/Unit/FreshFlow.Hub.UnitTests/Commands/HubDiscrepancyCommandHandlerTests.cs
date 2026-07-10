using FluentAssertions;
using FreshFlow.Hub.Application.Commands.AcknowledgeDiscrepancy;
using FreshFlow.Hub.Application.Commands.RecordDiscrepancy;
using FreshFlow.Hub.Application.Queries.ListDiscrepancies;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Hub.Domain.Events;
using FreshFlow.Hub.UnitTests.TestDoubles;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class HubDiscrepancyCommandHandlerTests
{
    [Fact]
    public async Task RecordDiscrepancy_ValidCommand_CreatesOpenDiscrepancyWithDomainEventAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inbounds = new InMemoryHubInboundRepository();
        var discrepancies = new InMemoryHubDiscrepancyRepository();
        var orders = new InMemoryOrderLookupReader();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var inbound = CreateInbound(hub.Id);
        var orderItemId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        await hubs.AddAsync(hub, default);
        await inbounds.AddAsync(inbound, default);
        orders.Add(orderItemId, orderId);
        var sut = new RecordDiscrepancyCommandHandler(hubs, inbounds, discrepancies, orders);

        var result = await sut.Handle(
            new RecordDiscrepancyCommand(
                hub.Id,
                inbound.Id,
                orderItemId,
                3m,
                HubDiscrepancy.ConditionPartial,
                "Short count"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(HubDiscrepancy.StatusOpen);
        result.Value.OrderId.Should().Be(orderId);
        discrepancies.Discrepancies.Should().ContainSingle();
        discrepancies.Discrepancies.Single().DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<HubDiscrepancyRecordedDomainEvent>();
        discrepancies.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordDiscrepancy_OrderItemMissing_ReturnsOrderItemNotFoundAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inbounds = new InMemoryHubInboundRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var inbound = CreateInbound(hub.Id);
        await hubs.AddAsync(hub, default);
        await inbounds.AddAsync(inbound, default);
        var discrepancies = new InMemoryHubDiscrepancyRepository();
        var sut = new RecordDiscrepancyCommandHandler(
            hubs,
            inbounds,
            discrepancies,
            new InMemoryOrderLookupReader());

        var result = await sut.Handle(
            new RecordDiscrepancyCommand(
                hub.Id,
                inbound.Id,
                Guid.NewGuid(),
                1m,
                HubDiscrepancy.ConditionMissing,
                null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ORDER_ITEM_NOT_FOUND");
        discrepancies.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task RecordDiscrepancy_InboundWrongHub_ReturnsInboundNotFoundAsync()
    {
        var hubs = new InMemoryHubRepository();
        var inbounds = new InMemoryHubInboundRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        await hubs.AddAsync(hub, default);
        await inbounds.AddAsync(CreateInbound(Guid.NewGuid()), default);
        var sut = new RecordDiscrepancyCommandHandler(
            hubs,
            inbounds,
            new InMemoryHubDiscrepancyRepository(),
            new InMemoryOrderLookupReader());

        var result = await sut.Handle(
            new RecordDiscrepancyCommand(
                hub.Id,
                inbounds.Inbounds.Single().Id,
                Guid.NewGuid(),
                1m,
                HubDiscrepancy.ConditionMissing,
                null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("HUB_INBOUND_EVENT_NOT_FOUND");
    }

    [Fact]
    public async Task AcknowledgeDiscrepancy_OpenDiscrepancy_AcknowledgesAndPersistsAsync()
    {
        var repo = new InMemoryHubDiscrepancyRepository();
        var discrepancy = CreateDiscrepancy();
        var adminUserId = Guid.NewGuid();
        await repo.AddAsync(discrepancy, default);
        var sut = new AcknowledgeDiscrepancyCommandHandler(repo);

        var result = await sut.Handle(
            new AcknowledgeDiscrepancyCommand(discrepancy.HubId, discrepancy.Id, adminUserId),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(HubDiscrepancy.StatusAcknowledged);
        result.Value.AcknowledgedBy.Should().Be(adminUserId);
        repo.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task AcknowledgeDiscrepancy_AlreadyAcknowledged_ReturnsConflictAsync()
    {
        var repo = new InMemoryHubDiscrepancyRepository();
        var discrepancy = CreateDiscrepancy();
        discrepancy.Acknowledge(Guid.NewGuid());
        await repo.AddAsync(discrepancy, default);
        var sut = new AcknowledgeDiscrepancyCommandHandler(repo);

        var result = await sut.Handle(
            new AcknowledgeDiscrepancyCommand(discrepancy.HubId, discrepancy.Id, Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DISCREPANCY_ALREADY_ACKNOWLEDGED");
        repo.SaveChangesCount.Should().Be(0);
    }

    [Fact]
    public async Task ListDiscrepancies_StatusFilter_ReturnsOnlyMatchingHubRowsAsync()
    {
        var hubs = new InMemoryHubRepository();
        var repo = new InMemoryHubDiscrepancyRepository();
        var hub = HubEntity.Create("Main Hub", null, null, null, 1000, null);
        var open = CreateDiscrepancy(hub.Id);
        var acknowledged = CreateDiscrepancy(hub.Id);
        acknowledged.Acknowledge(Guid.NewGuid());
        await hubs.AddAsync(hub, default);
        await repo.AddAsync(open, default);
        await repo.AddAsync(acknowledged, default);
        await repo.AddAsync(CreateDiscrepancy(Guid.NewGuid()), default);
        var sut = new ListDiscrepanciesQueryHandler(hubs, repo);

        var result = await sut.Handle(
            new ListDiscrepanciesQuery(hub.Id, HubDiscrepancy.StatusOpen),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle()
            .Which.DiscrepancyId.Should().Be(open.Id);
    }

    private static HubInboundEvent CreateInbound(Guid hubId) =>
        HubInboundEvent.Record(
            hubId,
            null,
            null,
            null,
            [new HubInboundItem(Guid.NewGuid(), null, 1m)],
            DateTime.UtcNow);

    private static HubDiscrepancy CreateDiscrepancy(Guid? hubId = null) =>
        HubDiscrepancy.Create(
            hubId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1m,
            HubDiscrepancy.ConditionMissing,
            null);
}
