using FluentAssertions;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Hub.Infrastructure.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Hub.UnitTests.Jobs;

[Trait("Category", "Unit")]
public sealed class HubInboundBackfillHostedServiceTests
{
    private readonly IHubProcurementPlanReader _procurement = Substitute.For<IHubProcurementPlanReader>();
    private readonly IHubInboundRepository _inbounds = Substitute.For<IHubInboundRepository>();

    [Fact]
    public async Task Run_NoHandedOffBatches_CreatesNothingAsync()
    {
        _procurement.ReadHandedOffBatchesAsync(Arg.Any<CancellationToken>()).Returns([]);

        await RunAsync();

        await _inbounds.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Run_BatchAlreadyRecorded_SkipsAsync()
    {
        var batch = CreateBatch();
        _procurement.ReadHandedOffBatchesAsync(Arg.Any<CancellationToken>()).Returns([batch]);
        _inbounds.DeliveryScheduleExistsAsync(batch.HubId, batch.BatchId, Arg.Any<CancellationToken>())
            .Returns(true);

        await RunAsync();

        await _procurement.DidNotReceiveWithAnyArgs().ReadBatchItemsAsync(default, default);
        await _inbounds.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Run_NoPurchasedItems_SkipsAsync()
    {
        var batch = CreateBatch();
        _procurement.ReadHandedOffBatchesAsync(Arg.Any<CancellationToken>()).Returns([batch]);
        _inbounds.DeliveryScheduleExistsAsync(batch.HubId, batch.BatchId, Arg.Any<CancellationToken>())
            .Returns(false);
        _procurement.ReadBatchItemsAsync(batch.BatchId, Arg.Any<CancellationToken>())
            .Returns([CreateItem(actualQuantity: null)]);

        await RunAsync();

        await _inbounds.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Run_PurchasedItems_CreatesPendingInboundAsync()
    {
        var batch = CreateBatch();
        var marketProductId = Guid.NewGuid();
        _procurement.ReadHandedOffBatchesAsync(Arg.Any<CancellationToken>()).Returns([batch]);
        _inbounds.DeliveryScheduleExistsAsync(batch.HubId, batch.BatchId, Arg.Any<CancellationToken>())
            .Returns(false);
        _procurement.ReadBatchItemsAsync(batch.BatchId, Arg.Any<CancellationToken>())
            .Returns([CreateItem(marketProductId, "Rau muong", actualQuantity: 4)]);

        await RunAsync();

        await _inbounds.Received(1).AddAsync(
            Arg.Is<HubInboundEvent>(inbound =>
                inbound.HubId == batch.HubId &&
                inbound.DeliveryScheduleId == batch.BatchId &&
                inbound.RecordedBy == batch.AssignedAgentUserId &&
                inbound.Items.Count == 1 &&
                inbound.Items[0].MarketProductId == marketProductId &&
                inbound.Items[0].QuantityKg == 4m &&
                inbound.Items[0].ProductName == "Rau muong"),
            Arg.Any<CancellationToken>());
        await _inbounds.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_ConcurrencyExceptionOnOneBatch_ContinuesWithNextAsync()
    {
        var failing = CreateBatch();
        var succeeding = CreateBatch();
        _procurement.ReadHandedOffBatchesAsync(Arg.Any<CancellationToken>()).Returns([failing, succeeding]);
        _inbounds.DeliveryScheduleExistsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _procurement.ReadBatchItemsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([CreateItem(actualQuantity: 1)]);
        _inbounds.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(
                Task.FromException(new HubConcurrencyException("dup", new Exception())),
                Task.CompletedTask);

        await RunAsync();

        await _inbounds.Received(2).AddAsync(Arg.Any<HubInboundEvent>(), Arg.Any<CancellationToken>());
        await _inbounds.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_ReaderThrowsForOneBatch_LogsAndContinuesAsync()
    {
        var failing = CreateBatch();
        var succeeding = CreateBatch();
        _procurement.ReadHandedOffBatchesAsync(Arg.Any<CancellationToken>()).Returns([failing, succeeding]);
        _inbounds.DeliveryScheduleExistsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _procurement.ReadBatchItemsAsync(failing.BatchId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<HubProcurementItemDto>>(new InvalidOperationException("boom")));
        _procurement.ReadBatchItemsAsync(succeeding.BatchId, Arg.Any<CancellationToken>())
            .Returns([CreateItem(actualQuantity: 1)]);

        var act = RunAsync;

        await act.Should().NotThrowAsync();
        await _inbounds.Received(1).AddAsync(Arg.Any<HubInboundEvent>(), Arg.Any<CancellationToken>());
    }

    private async Task RunAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_procurement);
        services.AddSingleton(_inbounds);
        await using var provider = services.BuildServiceProvider();

        var sut = new HubInboundBackfillHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<ILogger<HubInboundBackfillHostedService>>());

        await sut.RunBackfillAsync(default);
    }

    private static HubHandedOffBatchDto CreateBatch() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid());

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
