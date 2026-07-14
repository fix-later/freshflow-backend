using FluentAssertions;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Commands.AssignAgent;
using FreshFlow.Procurement.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class AssignAgentCommandTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 15, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Validator_EmptyIds_IsInvalid()
    {
        var result = new AssignAgentCommandValidator()
            .Validate(new AssignAgentCommand(Guid.Empty, Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handler_EligibleAgent_AssignsSavesAndReturnsBatchAsync()
    {
        var agentUserId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var batch = BuildManifestedBatch(orderId);
        batch.ClearDomainEvents();
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        repository.SaveChangesAsync(default).Returns(true);
        var marketAgents = Substitute.For<IMarketAgentReader>();
        marketAgents.IsEligibleMarketAgentAsync(
                agentUserId,
                batch.MarketId,
                default)
            .Returns(true);
        var orders = Substitute.For<IConfirmedOrderReader>();
        orders.ReadStatusesAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.SequenceEqual(new[] { orderId })),
                default)
            .Returns(new Dictionary<Guid, string> { [orderId] = "Batched" });
        var handler = new AssignAgentCommandHandler(
            repository,
            marketAgents,
            orders,
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new AssignAgentCommand(batch.Id, agentUserId),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Manifested");
        result.Value.AssignedAgentUserId.Should().Be(agentUserId);
        result.Value.AssignedAt.Should().Be(Now.UtcDateTime);
        await marketAgents.Received(1).IsEligibleMarketAgentAsync(
            agentUserId,
            batch.MarketId,
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
        var marketAgents = Substitute.For<IMarketAgentReader>();
        var handler = new AssignAgentCommandHandler(
            repository,
            marketAgents,
            Substitute.For<IConfirmedOrderReader>(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new AssignAgentCommand(batchId, Guid.NewGuid()),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PROCUREMENT_BATCH_NOT_FOUND");
        await marketAgents.DidNotReceiveWithAnyArgs()
            .IsEligibleMarketAgentAsync(default, default, default);
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handler_IneligibleAgent_ReturnsValidationAsync()
    {
        var agentUserId = Guid.NewGuid();
        var batch = BuildManifestedBatch(Guid.NewGuid());
        batch.ClearDomainEvents();
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        var marketAgents = Substitute.For<IMarketAgentReader>();
        marketAgents.IsEligibleMarketAgentAsync(
                agentUserId,
                batch.MarketId,
                default)
            .Returns(false);
        var handler = new AssignAgentCommandHandler(
            repository,
            marketAgents,
            Substitute.For<IConfirmedOrderReader>(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new AssignAgentCommand(batch.Id, agentUserId),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AGENT_NOT_ELIGIBLE");
        batch.AssignedAgentUserId.Should().BeNull();
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    private static ProcurementBatch BuildManifestedBatch(Guid orderId)
    {
        var productId = Guid.NewGuid();
        var batch = ProcurementBatch.Build(
            new DateOnly(2026, 7, 16),
            Guid.NewGuid(),
            [(productId, "Tomato", 5, orderId)])
            .Value;
        batch.Manifest(
            new Dictionary<Guid, decimal> { [productId] = 10_000m },
            new DateTime(2026, 7, 15, 1, 0, 0, DateTimeKind.Utc));
        return batch;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
