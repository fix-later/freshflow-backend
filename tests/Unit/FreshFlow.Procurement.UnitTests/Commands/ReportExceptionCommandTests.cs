using FluentAssertions;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Commands.ReportException;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class ReportExceptionCommandTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 15, 3, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Validator_ValidZeroQuantityAndCloudinaryUrl_IsValid()
    {
        var result = new ReportExceptionCommandValidator().Validate(
            new ReportExceptionCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Unavailable",
                0,
                null,
                "https://res.cloudinary.com/freshflow/image/upload/proof.jpg"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_InvalidFields_IsInvalid()
    {
        var result = new ReportExceptionCommandValidator().Validate(
            new ReportExceptionCommand(
                Guid.Empty,
                Guid.Empty,
                Guid.Empty,
                "Unknown",
                -1,
                new string('n', 501),
                "http://example.com/proof.jpg"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(7);
    }

    [Fact]
    public async Task Handler_OwnerReports_SavesAndReturnsUpdatedBatchAsync()
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
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { orderId })),
                default)
            .Returns(new Dictionary<Guid, string> { [orderId] = "Batched" });
        var handler = new ReportExceptionCommandHandler(
            repository,
            orders,
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new ReportExceptionCommand(
                batch.Id,
                agentUserId,
                productId,
                "Unavailable",
                0,
                "Sold out",
                null),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Exceptions.Should().ContainSingle(exception =>
            exception.MarketProductId == productId &&
            exception.Type == "Unavailable" &&
            exception.ReportedQuantity == 0 &&
            exception.ReportedAt == Now.UtcDateTime);
        result.Value.Members.Should().ContainSingle()
            .Which.Status.Should().Be("Batched");
        await repository.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handler_UnknownBatch_ReturnsNotFoundAsync()
    {
        var batchId = Guid.NewGuid();
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batchId, default).Returns((ProcurementBatch?)null);
        var handler = new ReportExceptionCommandHandler(
            repository,
            Substitute.For<IConfirmedOrderReader>(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            ValidCommand(batchId, Guid.NewGuid(), Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PROCUREMENT_BATCH_NOT_FOUND");
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handler_DifferentAgent_ReturnsNotFoundAsync()
    {
        var batch = BuildAssignedBatch(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        var handler = new ReportExceptionCommandHandler(
            repository,
            Substitute.For<IConfirmedOrderReader>(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            ValidCommand(batch.Id, Guid.NewGuid(), batch.Items.Single().MarketProductId),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PROCUREMENT_BATCH_NOT_FOUND");
        batch.Exceptions.Should().BeEmpty();
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handler_BuiltBatch_PropagatesConflictAsync()
    {
        var agentUserId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var batch = ProcurementBatch.Build(
            new DateOnly(2026, 7, 15),
            Guid.NewGuid(),
            [(productId, "Tomato", 2, Guid.NewGuid())])
            .Value;
        typeof(ProcurementBatchItem).GetProperty(nameof(ProcurementBatchItem.AssignedAgentUserId))!
            .SetValue(batch.Items.Single(), agentUserId);
        batch.ClearDomainEvents();
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        var handler = new ReportExceptionCommandHandler(
            repository,
            Substitute.For<IConfirmedOrderReader>(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            ValidCommand(batch.Id, agentUserId, productId),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_NOT_REPORTABLE");
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handler_InvalidType_ReturnsValidationAsync()
    {
        var agentUserId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var batch = BuildAssignedBatch(agentUserId, productId, Guid.NewGuid());
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        var handler = new ReportExceptionCommandHandler(
            repository,
            Substitute.For<IConfirmedOrderReader>(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            ValidCommand(batch.Id, agentUserId, productId) with { Type = "Unknown" },
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_EXCEPTION_TYPE");
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    private static ReportExceptionCommand ValidCommand(
        Guid batchId,
        Guid agentUserId,
        Guid productId) =>
        new(batchId, agentUserId, productId, "Unavailable", 0, null, null);

    private static ProcurementBatch BuildAssignedBatch(
        Guid agentUserId,
        Guid productId,
        Guid orderId)
    {
        var batch = ProcurementBatch.Build(
            new DateOnly(2026, 7, 15),
            Guid.NewGuid(),
            [(productId, "Tomato", 2, orderId)])
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
