using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.EventHandlers;
using FreshFlow.Hub.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Hub.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class ProcurementBatchHandedOffIntegrationEventHandlerTests
{
    private readonly IHubProcurementPlanReader _procurement = Substitute.For<IHubProcurementPlanReader>();
    private readonly IHubInboundRepository _inbounds = Substitute.For<IHubInboundRepository>();

    [Fact]
    public async Task Handle_NoHubId_SkipsWithoutTouchingRepositoriesAsync()
    {
        var evt = CreateEvent(withHub: false);

        await CreateSut().Handle(evt, default);

        await _procurement.DidNotReceiveWithAnyArgs().ReadBatchItemsAsync(default, default);
        await _inbounds.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Handle_NoPurchasedItems_SkipsWithoutCreatingInboundAsync()
    {
        var evt = CreateEvent();
        _procurement.ReadBatchItemsAsync(evt.BatchId, default)
            .Returns([CreateItem(actualQuantity: null), CreateItem(actualQuantity: 0)]);

        await CreateSut().Handle(evt, default);

        await _inbounds.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Handle_AlreadyRecorded_IsIdempotentAsync()
    {
        var evt = CreateEvent();
        _procurement.ReadBatchItemsAsync(evt.BatchId, default)
            .Returns([CreateItem(actualQuantity: 3)]);
        _inbounds.DeliveryScheduleExistsAsync(evt.HubId!.Value, evt.BatchId, default)
            .Returns(true);

        await CreateSut().Handle(evt, default);

        await _inbounds.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Handle_PurchasedItems_CreatesPendingInboundWithMappedItemsAsync()
    {
        var evt = CreateEvent();
        var marketProductId = Guid.NewGuid();
        _procurement.ReadBatchItemsAsync(evt.BatchId, default)
            .Returns([CreateItem(marketProductId, "Rau muong", actualQuantity: 5)]);
        _inbounds.DeliveryScheduleExistsAsync(evt.HubId!.Value, evt.BatchId, default)
            .Returns(false);

        await CreateSut().Handle(evt, default);

        await _inbounds.Received(1).AddAsync(
            Arg.Is<HubInboundEvent>(inbound =>
                inbound.HubId == evt.HubId &&
                inbound.SourceMarketId == evt.MarketId &&
                inbound.DeliveryScheduleId == evt.BatchId &&
                inbound.Status == HubInboundEvent.StatusPending &&
                inbound.RecordedBy == evt.HandedOffByUserId &&
                inbound.Items.Count == 1 &&
                inbound.Items[0].MarketProductId == marketProductId &&
                inbound.Items[0].ProductId == null &&
                inbound.Items[0].QuantityKg == 5m &&
                inbound.Items[0].ProductName == "Rau muong"),
            default);
        await _inbounds.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handle_ConcurrencyExceptionOnSave_IsSwallowedAsync()
    {
        var evt = CreateEvent();
        _procurement.ReadBatchItemsAsync(evt.BatchId, default)
            .Returns([CreateItem(actualQuantity: 2)]);
        _inbounds.DeliveryScheduleExistsAsync(evt.HubId!.Value, evt.BatchId, default)
            .Returns(false);
        _inbounds.SaveChangesAsync(default)
            .Returns(Task.FromException(new HubConcurrencyException("dup", new Exception())));

        var act = () => CreateSut().Handle(evt, default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_ReaderThrows_IsLoggedAndSwallowedAsync()
    {
        var evt = CreateEvent();
        _procurement.ReadBatchItemsAsync(evt.BatchId, default)
            .Returns(Task.FromException<IReadOnlyList<HubProcurementItemDto>>(new InvalidOperationException("boom")));

        var act = () => CreateSut().Handle(evt, default);

        await act.Should().NotThrowAsync();
        await _inbounds.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    private ProcurementBatchHandedOffIntegrationEventHandler CreateSut() =>
        new(
            _procurement,
            _inbounds,
            Substitute.For<ILogger<ProcurementBatchHandedOffIntegrationEventHandler>>());

    private static ProcurementBatchHandedOffIntegrationEvent CreateEvent(bool withHub = true) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            withHub ? Guid.NewGuid() : null,
            new DateTime(2026, 7, 27, 4, 0, 0, DateTimeKind.Utc),
            [],
            Guid.NewGuid());

    private static HubProcurementItemDto CreateItem(
        Guid? marketProductId = null,
        string productName = "Product",
        int? actualQuantity = 1) =>
        new(
            marketProductId ?? Guid.NewGuid(),
            productName,
            TargetQuantity: 10,
            actualQuantity,
            ActualUnitPrice: null,
            PurchasedAt: null);
}
