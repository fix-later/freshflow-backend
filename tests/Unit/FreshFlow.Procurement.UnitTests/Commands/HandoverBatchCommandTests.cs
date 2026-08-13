using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Commands.HandoverBatch;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using MediatR;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class HandoverBatchCommandTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 15, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Validator_EmptyIds_IsInvalid()
    {
        var result = new HandoverBatchCommandValidator().Validate(
            new HandoverBatchCommand(Guid.Empty, Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }


    [Fact]
    public async Task Handler_OwnerHandsOver_SavesAndReturnsBatchAsync()
    {
        var agentUserId = Guid.NewGuid();
        var hubId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var batch = BuildAssignedBatch(agentUserId, orderId, purchased: true, hubId);
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        repository.SaveChangesAsync(default).Returns(true);
        ExecuteOperations(repository);
        var orders = Substitute.For<IConfirmedOrderReader>();
        orders.ReadStatusesAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.SequenceEqual(new[] { orderId })),
                default)
            .Returns(new Dictionary<Guid, string> { [orderId] = "AtHub" });
        var finalizer = Substitute.For<IProcurementHandoverOrderFinalizer>();
        var publisher = Substitute.For<IPublisher>();
        var handler = new HandoverBatchCommandHandler(
            repository,
            orders,
            ActiveHubReader(),
            finalizer,
            publisher,
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new HandoverBatchCommand(batch.Id, agentUserId),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("HandedOff");
        result.Value.HandedOffAt.Should().Be(Now.UtcDateTime);
        result.Value.HubId.Should().Be(hubId);
        result.Value.Members.Should().ContainSingle()
            .Which.Status.Should().Be("AtHub");
        await finalizer.Received(1).FinalizeAsync(
            Arg.Is<ProcurementBatchHandedOffIntegrationEvent>(value =>
                value.BatchId == batch.Id && value.CoveredOrderIds.SequenceEqual(new[] { orderId })),
            default);
        await publisher.Received(1).Publish(
            Arg.Any<ProcurementBatchHandedOffIntegrationEvent>(),
            default);
        await repository.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handler_UnknownBatch_ReturnsNotFoundAsync()
    {
        var batchId = Guid.NewGuid();
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batchId, default)
            .Returns((ProcurementBatch?)null);
        var handler = new HandoverBatchCommandHandler(
            repository,
            Substitute.For<IConfirmedOrderReader>(),
            ActiveHubReader(),
            Substitute.For<IProcurementHandoverOrderFinalizer>(),
            Substitute.For<IPublisher>(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new HandoverBatchCommand(batchId, Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PROCUREMENT_BATCH_NOT_FOUND");
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handler_DifferentAgent_ReturnsNotFoundAsync()
    {
        var batch = BuildAssignedBatch(Guid.NewGuid(), Guid.NewGuid(), purchased: true);
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        var handler = new HandoverBatchCommandHandler(
            repository,
            Substitute.For<IConfirmedOrderReader>(),
            ActiveHubReader(),
            Substitute.For<IProcurementHandoverOrderFinalizer>(),
            Substitute.For<IPublisher>(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new HandoverBatchCommand(batch.Id, Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PROCUREMENT_BATCH_NOT_FOUND");
        batch.Status.Should().Be(ProcurementBatchStatus.Purchasing);
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handler_InactiveHub_RejectsHandoverAsync()
    {
        var agentUserId = Guid.NewGuid();
        var batch = BuildAssignedBatch(agentUserId, Guid.NewGuid(), purchased: true);
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        var handler = new HandoverBatchCommandHandler(
            repository,
            Substitute.For<IConfirmedOrderReader>(),
            ActiveHubReader(isActive: false),
            Substitute.For<IProcurementHandoverOrderFinalizer>(),
            Substitute.For<IPublisher>(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new HandoverBatchCommand(batch.Id, agentUserId),
            default);

        result.Error.Code.Should().Be("HUB_INACTIVE");
        batch.Status.Should().Be(ProcurementBatchStatus.Purchasing);
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handler_LegacyBatchWithoutHub_RejectsHandoverAsync()
    {
        var agentUserId = Guid.NewGuid();
        var batch = BuildAssignedBatch(
            agentUserId, Guid.NewGuid(), purchased: true, hasHub: false);
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        var handler = new HandoverBatchCommandHandler(
            repository,
            Substitute.For<IConfirmedOrderReader>(),
            ActiveHubReader(),
            Substitute.For<IProcurementHandoverOrderFinalizer>(),
            Substitute.For<IPublisher>(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new HandoverBatchCommand(batch.Id, agentUserId),
            default);

        result.Error.Code.Should().Be("HUB_NOT_CONFIGURED_FOR_MARKET");
        batch.Status.Should().Be(ProcurementBatchStatus.Purchasing);
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handler_ManifestedBatch_PropagatesConflictAsync()
    {
        var agentUserId = Guid.NewGuid();
        var batch = BuildAssignedBatch(agentUserId, Guid.NewGuid(), purchased: false);
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        var handler = new HandoverBatchCommandHandler(
            repository,
            Substitute.For<IConfirmedOrderReader>(),
            ActiveHubReader(),
            Substitute.For<IProcurementHandoverOrderFinalizer>(),
            Substitute.For<IPublisher>(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new HandoverBatchCommand(batch.Id, agentUserId),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_NOT_PURCHASED");
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    private static ProcurementBatch BuildAssignedBatch(
        Guid agentUserId,
        Guid orderId,
        bool purchased,
        Guid? hubId = null,
        bool hasHub = true)
    {
        var productId = Guid.NewGuid();
        var date = new DateOnly(2026, 7, 16);
        var marketId = Guid.NewGuid();
        var lines = new[] { (productId, "Tomato", 5, orderId) };
        var batch = hasHub
            ? ProcurementBatch.Build(date, marketId, lines, hubId ?? Guid.NewGuid()).Value
            : ProcurementBatch.Build(date, marketId, lines).Value;
        batch.Manifest(
            new Dictionary<Guid, decimal> { [productId] = 10_000m },
            Now.UtcDateTime.AddHours(-3));
        batch.AssignItems(new Dictionary<Guid, Guid> { [productId] = agentUserId }, Now.UtcDateTime.AddHours(-2));
        if (purchased)
        {
            batch.ConfirmPurchase(
                agentUserId,
                new Dictionary<Guid, (int, decimal)> { [productId] = (5, 11_000m) },
                Now.UtcDateTime.AddHours(-1));
        }

        batch.ClearDomainEvents();
        return batch;
    }

    private static IHubByMarketReader ActiveHubReader(bool isActive = true)
    {
        var reader = Substitute.For<IHubByMarketReader>();
        reader.IsActiveAsync(Arg.Any<Guid>(), default)
            .Returns(isActive);
        return reader;
    }

    private static void ExecuteOperations(IProcurementBatchRepository repository) =>
        repository.ExecuteInSerializableTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<Result>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task<Result>>>(0)(
                call.ArgAt<CancellationToken>(1)));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
