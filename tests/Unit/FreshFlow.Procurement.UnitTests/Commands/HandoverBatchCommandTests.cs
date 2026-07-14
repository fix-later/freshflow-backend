using FluentAssertions;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Commands.HandoverBatch;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
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
            new HandoverBatchCommand(Guid.Empty, Guid.Empty, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void Validator_EmptyHubId_IsInvalid()
    {
        var result = new HandoverBatchCommandValidator().Validate(
            new HandoverBatchCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Handler_OwnerHandsOver_SavesAndReturnsBatchAsync()
    {
        var agentUserId = Guid.NewGuid();
        var hubId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var batch = BuildAssignedBatch(agentUserId, orderId, purchased: true);
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        repository.SaveChangesAsync(default).Returns(true);
        var orders = Substitute.For<IConfirmedOrderReader>();
        orders.ReadStatusesAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.SequenceEqual(new[] { orderId })),
                default)
            .Returns(new Dictionary<Guid, string> { [orderId] = "AtHub" });
        var handler = new HandoverBatchCommandHandler(
            repository,
            orders,
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new HandoverBatchCommand(batch.Id, agentUserId, hubId),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("HandedOff");
        result.Value.HandedOffAt.Should().Be(Now.UtcDateTime);
        result.Value.HubId.Should().Be(hubId);
        result.Value.Members.Should().ContainSingle()
            .Which.Status.Should().Be("AtHub");
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
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new HandoverBatchCommand(batchId, Guid.NewGuid(), null),
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
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new HandoverBatchCommand(batch.Id, Guid.NewGuid(), null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PROCUREMENT_BATCH_NOT_FOUND");
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
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new HandoverBatchCommand(batch.Id, agentUserId, null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_NOT_PURCHASED");
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    private static ProcurementBatch BuildAssignedBatch(
        Guid agentUserId,
        Guid orderId,
        bool purchased)
    {
        var productId = Guid.NewGuid();
        var batch = ProcurementBatch.Build(
            new DateOnly(2026, 7, 16),
            Guid.NewGuid(),
            [(productId, "Tomato", 5, orderId)])
            .Value;
        batch.Manifest(
            new Dictionary<Guid, decimal> { [productId] = 10_000m },
            Now.UtcDateTime.AddHours(-3));
        batch.AssignAgent(agentUserId, Now.UtcDateTime.AddHours(-2));
        if (purchased)
        {
            batch.ConfirmPurchase(
                new Dictionary<Guid, (int, decimal)> { [productId] = (5, 11_000m) },
                Now.UtcDateTime.AddHours(-1));
        }

        batch.ClearDomainEvents();
        return batch;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
