using FluentAssertions;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.Procurement.Domain.Events;

namespace FreshFlow.Procurement.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class ProcurementBatchTests
{
    [Fact]
    public void Build_RepeatedProductAcrossOrders_AggregatesQuantityAndCoverage()
    {
        var marketId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var firstOrderId = Guid.NewGuid();
        var secondOrderId = Guid.NewGuid();

        var result = ProcurementBatch.Build(
            new DateOnly(2026, 7, 15),
            marketId,
            [
                (productId, "Cabbage", 4, firstOrderId),
                (productId, "Cabbage", 6, secondOrderId),
                (productId, "Cabbage", 1, firstOrderId)
            ]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(ProcurementBatchStatus.Built);
        result.Value.TotalItemCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle()
            .Which.TotalQuantity.Should().Be(11);
        result.Value.Orders.Select(order => order.OrderId)
            .Should().BeEquivalentTo([firstOrderId, secondOrderId]);
    }

    [Fact]
    public void Build_ValidLines_RaisesBuiltEvent()
    {
        var marketId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var result = ProcurementBatch.Build(
            new DateOnly(2026, 7, 15),
            marketId,
            [(Guid.NewGuid(), "Tomato", 2, orderId)]);

        var domainEvent = result.Value.DomainEvents
            .Should().ContainSingle()
            .Which.Should().BeOfType<ProcurementBatchBuiltDomainEvent>().Subject;
        domainEvent.BatchId.Should().Be(result.Value.Id);
        domainEvent.MarketId.Should().Be(marketId);
        domainEvent.CoveredOrderIds.Should().Equal(orderId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Build_NonPositiveQuantity_ReturnsFailure(int quantity)
    {
        var result = ProcurementBatch.Build(
            new DateOnly(2026, 7, 15),
            Guid.NewGuid(),
            [(Guid.NewGuid(), "Tomato", quantity, Guid.NewGuid())]);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_PROCUREMENT_BATCH");
    }

    [Fact]
    public void Build_EmptyLines_ReturnsFailure()
    {
        var result = ProcurementBatch.Build(
            new DateOnly(2026, 7, 15),
            Guid.NewGuid(),
            []);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Manifest_BuiltBatch_FreezesPricesAndRaisesEvent()
    {
        var firstProductId = Guid.NewGuid();
        var secondProductId = Guid.NewGuid();
        var batch = BuildBatch(firstProductId, secondProductId);
        var manifestedAt = new DateTime(2026, 7, 14, 16, 0, 0, DateTimeKind.Utc);
        batch.ClearDomainEvents();

        var result = batch.Manifest(
            new Dictionary<Guid, decimal>
            {
                [firstProductId] = 12_500m,
                [secondProductId] = 8_750m
            },
            manifestedAt);

        result.IsSuccess.Should().BeTrue();
        batch.Status.Should().Be(ProcurementBatchStatus.Manifested);
        batch.ManifestedAt.Should().Be(manifestedAt);
        batch.Items.Should().Contain(item =>
            item.MarketProductId == firstProductId && item.ReferenceUnitPrice == 12_500m);
        batch.Items.Should().Contain(item =>
            item.MarketProductId == secondProductId && item.ReferenceUnitPrice == 8_750m);
        var domainEvent = batch.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProcurementManifestGeneratedDomainEvent>().Subject;
        domainEvent.ManifestedAt.Should().Be(manifestedAt);
    }

    [Fact]
    public void Manifest_ManifestedBatch_RefreshesPricesAndTimestamp()
    {
        var productId = Guid.NewGuid();
        var batch = BuildBatch(productId);
        var firstCapture = new DateTime(2026, 7, 14, 16, 0, 0, DateTimeKind.Utc);
        var secondCapture = firstCapture.AddMinutes(10);
        batch.Manifest(new Dictionary<Guid, decimal> { [productId] = 10_000m }, firstCapture);
        batch.ClearDomainEvents();

        var result = batch.Manifest(
            new Dictionary<Guid, decimal> { [productId] = 11_000m },
            secondCapture);

        result.IsSuccess.Should().BeTrue();
        batch.Status.Should().Be(ProcurementBatchStatus.Manifested);
        batch.ManifestedAt.Should().Be(secondCapture);
        batch.Items.Should().ContainSingle()
            .Which.ReferenceUnitPrice.Should().Be(11_000m);
        batch.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProcurementManifestGeneratedDomainEvent>();
    }

    [Theory]
    [InlineData(ProcurementBatchStatus.Purchasing)]
    [InlineData(ProcurementBatchStatus.HandedOff)]
    public void Manifest_InProgressBatch_ReturnsConflict(ProcurementBatchStatus status)
    {
        var productId = Guid.NewGuid();
        var batch = BuildBatch(productId);
        typeof(ProcurementBatch).GetProperty(nameof(ProcurementBatch.Status))!
            .SetValue(batch, status);
        batch.ClearDomainEvents();

        var result = batch.Manifest(
            new Dictionary<Guid, decimal> { [productId] = 10_000m },
            DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_NOT_MANIFESTABLE");
        batch.Status.Should().Be(status);
        batch.ManifestedAt.Should().BeNull();
        batch.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Manifest_MissingReferencePrice_ReturnsValidationWithoutMutation()
    {
        var firstProductId = Guid.NewGuid();
        var secondProductId = Guid.NewGuid();
        var batch = BuildBatch(firstProductId, secondProductId);
        batch.ClearDomainEvents();

        var result = batch.Manifest(
            new Dictionary<Guid, decimal> { [firstProductId] = 10_000m },
            DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("REFERENCE_PRICE_MISSING");
        batch.Status.Should().Be(ProcurementBatchStatus.Built);
        batch.ManifestedAt.Should().BeNull();
        batch.Items.Should().OnlyContain(item => item.ReferenceUnitPrice == null);
        batch.DomainEvents.Should().BeEmpty();
    }


    [Fact]
    public void AssignAgent_ManifestedBatch_SetsAssignmentRaisesEventAndKeepsStatus()
    {
        var batch = BuildManifestedBatch();
        var agentUserId = Guid.NewGuid();
        var assignedAt = new DateTime(2026, 7, 15, 2, 0, 0, DateTimeKind.Utc);
        batch.ClearDomainEvents();

        var result = batch.AssignAgent(agentUserId, assignedAt);

        result.IsSuccess.Should().BeTrue();
        batch.Status.Should().Be(ProcurementBatchStatus.Manifested);
        batch.AssignedAgentUserId.Should().Be(agentUserId);
        batch.AssignedAt.Should().Be(assignedAt);
        var domainEvent = batch.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProcurementAgentAssignedDomainEvent>().Subject;
        domainEvent.BatchId.Should().Be(batch.Id);
        domainEvent.MarketId.Should().Be(batch.MarketId);
        domainEvent.AgentUserId.Should().Be(agentUserId);
        domainEvent.AssignedAt.Should().Be(assignedAt);
    }

    [Fact]
    public void AssignAgent_ManifestedBatch_ReassignsAndRefreshesTimestamp()
    {
        var batch = BuildManifestedBatch();
        var firstAgentId = Guid.NewGuid();
        var secondAgentId = Guid.NewGuid();
        var firstAssignment = new DateTime(2026, 7, 15, 2, 0, 0, DateTimeKind.Utc);
        var secondAssignment = firstAssignment.AddMinutes(15);
        batch.AssignAgent(firstAgentId, firstAssignment);
        batch.ClearDomainEvents();

        var result = batch.AssignAgent(secondAgentId, secondAssignment);

        result.IsSuccess.Should().BeTrue();
        batch.Status.Should().Be(ProcurementBatchStatus.Manifested);
        batch.AssignedAgentUserId.Should().Be(secondAgentId);
        batch.AssignedAt.Should().Be(secondAssignment);
        batch.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProcurementAgentAssignedDomainEvent>();
    }

    [Fact]
    public void AssignAgent_BuiltBatch_ReturnsNotManifestedConflict()
    {
        var batch = BuildBatch(Guid.NewGuid());
        batch.ClearDomainEvents();

        var result = batch.AssignAgent(Guid.NewGuid(), DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_NOT_MANIFESTED");
        batch.AssignedAgentUserId.Should().BeNull();
        batch.AssignedAt.Should().BeNull();
        batch.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ProcurementBatchStatus.Purchasing)]
    [InlineData(ProcurementBatchStatus.HandedOff)]
    public void AssignAgent_InProgressBatch_ReturnsConflict(ProcurementBatchStatus status)
    {
        var batch = BuildManifestedBatch();
        typeof(ProcurementBatch).GetProperty(nameof(ProcurementBatch.Status))!
            .SetValue(batch, status);
        batch.ClearDomainEvents();

        var result = batch.AssignAgent(Guid.NewGuid(), DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_ALREADY_IN_PROGRESS");
        batch.Status.Should().Be(status);
        batch.AssignedAgentUserId.Should().BeNull();
        batch.AssignedAt.Should().BeNull();
        batch.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AssignAgent_EmptyAgentId_ReturnsValidation()
    {
        var batch = BuildManifestedBatch();
        batch.ClearDomainEvents();

        var result = batch.AssignAgent(Guid.Empty, DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_AGENT");
        batch.AssignedAgentUserId.Should().BeNull();
        batch.DomainEvents.Should().BeEmpty();
    }

    private static ProcurementBatch BuildManifestedBatch()
    {
        var productId = Guid.NewGuid();
        var batch = BuildBatch(productId);
        batch.Manifest(
            new Dictionary<Guid, decimal> { [productId] = 10_000m },
            new DateTime(2026, 7, 15, 1, 0, 0, DateTimeKind.Utc));
        return batch;
    }

    private static ProcurementBatch BuildBatch(params Guid[] marketProductIds) =>
        ProcurementBatch.Build(
            new DateOnly(2026, 7, 15),
            Guid.NewGuid(),
            marketProductIds.Select(id =>
                (id, $"Product {id}", 2, Guid.NewGuid())))
        .Value;
}
