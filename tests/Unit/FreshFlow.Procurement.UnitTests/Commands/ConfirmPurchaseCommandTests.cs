using FluentAssertions;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Commands.ConfirmPurchase;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ConfirmPurchaseCommandTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 15, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Validator_EmptyIdsAndLines_IsInvalid()
    {
        var result = new ConfirmPurchaseCommandValidator().Validate(
            new ConfirmPurchaseCommand(Guid.Empty, Guid.Empty, []));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void Validator_InvalidAndDuplicateLines_IsInvalid()
    {
        var productId = Guid.NewGuid();
        var result = new ConfirmPurchaseCommandValidator().Validate(
            new ConfirmPurchaseCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                [
                    new PurchaseLineDto(productId, 0, 1m),
                    new PurchaseLineDto(productId, 1, 0m)
                ]));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handler_OwnerConfirms_SavesAndReturnsUpdatedBatchAsync()
    {
        var agentUserId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var batch = BuildAssignedBatch(agentUserId, productId, orderId);
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        repository.SaveChangesAsync(default).Returns(true);
        var orders = Substitute.For<IConfirmedOrderReader>();
        orders.ReadStatusesAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.SequenceEqual(new[] { orderId })),
                default)
            .Returns(new Dictionary<Guid, string> { [orderId] = "Batched" });
        var handler = new ConfirmPurchaseCommandHandler(
            repository,
            orders,
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new ConfirmPurchaseCommand(
                batch.Id,
                agentUserId,
                [new PurchaseLineDto(productId, 4, 11_000m)]),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Purchasing");
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.ActualQuantity.Should().Be(4);
        item.ActualUnitPrice.Should().Be(11_000m);
        item.PurchasedAt.Should().Be(Now.UtcDateTime);
        result.Value.Members.Should().ContainSingle()
            .Which.Status.Should().Be("Batched");
        await repository.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handler_UnknownBatch_ReturnsNotFoundAsync()
    {
        var batchId = Guid.NewGuid();
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batchId, default)
            .Returns((ProcurementBatch?)null);
        var handler = new ConfirmPurchaseCommandHandler(
            repository,
            Substitute.For<IConfirmedOrderReader>(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new ConfirmPurchaseCommand(
                batchId,
                Guid.NewGuid(),
                [new PurchaseLineDto(Guid.NewGuid(), 1, 1m)]),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PROCUREMENT_BATCH_NOT_FOUND");
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handler_DifferentAgent_ReturnsNotFoundAsync()
    {
        var ownerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var batch = BuildAssignedBatch(ownerId, productId, Guid.NewGuid());
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        var handler = new ConfirmPurchaseCommandHandler(
            repository,
            Substitute.For<IConfirmedOrderReader>(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new ConfirmPurchaseCommand(
                batch.Id,
                Guid.NewGuid(),
                [new PurchaseLineDto(productId, 1, 1m)]),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PROCUREMENT_BATCH_NOT_FOUND");
        batch.Status.Should().Be(ProcurementBatchStatus.Manifested);
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handler_BuiltBatch_PropagatesConflictAsync()
    {
        var agentUserId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var batch = ProcurementBatch.Build(
            new DateOnly(2026, 7, 16),
            Guid.NewGuid(),
            [(productId, "Tomato", 5, Guid.NewGuid())])
            .Value;
        typeof(ProcurementBatchItem)
            .GetProperty(nameof(ProcurementBatchItem.AssignedAgentUserId))!
            .SetValue(batch.Items.Single(), agentUserId);
        batch.ClearDomainEvents();
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        var handler = new ConfirmPurchaseCommandHandler(
            repository,
            Substitute.For<IConfirmedOrderReader>(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new ConfirmPurchaseCommand(
                batch.Id,
                agentUserId,
                [new PurchaseLineDto(productId, 5, 10_000m)]),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_NOT_MANIFESTED");
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handler_DuplicateLines_ReturnsValidationAsync()
    {
        var agentUserId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var batch = BuildAssignedBatch(agentUserId, productId, Guid.NewGuid());
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        var handler = new ConfirmPurchaseCommandHandler(
            repository,
            Substitute.For<IConfirmedOrderReader>(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new ConfirmPurchaseCommand(
                batch.Id,
                agentUserId,
                [
                    new PurchaseLineDto(productId, 1, 1m),
                    new PurchaseLineDto(productId, 2, 2m)
                ]),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PURCHASE_LINES_MISMATCH");
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    private static ProcurementBatch BuildAssignedBatch(
        Guid agentUserId,
        Guid productId,
        Guid orderId)
    {
        var batch = ProcurementBatch.Build(
            new DateOnly(2026, 7, 16),
            Guid.NewGuid(),
            [(productId, "Tomato", 5, orderId)])
            .Value;
        batch.Manifest(
            new Dictionary<Guid, decimal> { [productId] = 10_000m },
            Now.UtcDateTime.AddHours(-2));
        batch.AssignItems(new Dictionary<Guid, Guid> { [productId] = agentUserId }, Now.UtcDateTime.AddHours(-1));
        batch.ClearDomainEvents();
        return batch;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
