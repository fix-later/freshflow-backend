using FluentAssertions;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.Procurement.Domain.Events;

namespace FreshFlow.Procurement.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class ProcurementBatchMultiAgentTests
{
    private static readonly DateTime ManifestedAt =
        new(2026, 8, 12, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime AssignedAt = ManifestedAt.AddHours(1);

    [Fact]
    public void AssignItems_DistributesClearsAndRaisesOneEventPerNewAgent()
    {
        var firstProductId = Guid.NewGuid();
        var secondProductId = Guid.NewGuid();
        var firstAgentId = Guid.NewGuid();
        var secondAgentId = Guid.NewGuid();
        var batch = BuildManifestedBatch(firstProductId, secondProductId);
        batch.ClearDomainEvents();

        var assigned = batch.AssignItems(
            new Dictionary<Guid, Guid>
            {
                [firstProductId] = firstAgentId,
                [secondProductId] = secondAgentId
            },
            AssignedAt);

        assigned.IsSuccess.Should().BeTrue();
        batch.Items.Single(item => item.MarketProductId == firstProductId)
            .AssignedAgentUserId.Should().Be(firstAgentId);
        batch.Items.Single(item => item.MarketProductId == secondProductId)
            .AssignedAgentUserId.Should().Be(secondAgentId);
        batch.Items.Should().OnlyContain(item => item.AssignedAt == AssignedAt);
        batch.DomainEvents.OfType<ProcurementAgentAssignedDomainEvent>()
            .Select(domainEvent => domainEvent.AgentUserId)
            .Should().BeEquivalentTo(new[] { firstAgentId, secondAgentId });

        batch.ClearDomainEvents();
        var cleared = batch.AssignItems(
            new Dictionary<Guid, Guid> { [firstProductId] = Guid.Empty },
            AssignedAt.AddMinutes(5));

        cleared.IsSuccess.Should().BeTrue();
        batch.Items.Single(item => item.MarketProductId == firstProductId)
            .AssignedAgentUserId.Should().BeNull();
        batch.Items.Single(item => item.MarketProductId == firstProductId)
            .AssignedAt.Should().BeNull();
        batch.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AssignItems_UnknownOrPurchasedItem_ReturnsBusinessError()
    {
        var productId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var batch = BuildManifestedBatch(productId);

        var unknown = batch.AssignItems(
            new Dictionary<Guid, Guid> { [Guid.NewGuid()] = agentId },
            AssignedAt);

        unknown.IsFailure.Should().BeTrue();
        unknown.Error.Code.Should().Be("PRODUCT_NOT_IN_BATCH");

        batch.AssignItems(new Dictionary<Guid, Guid> { [productId] = agentId }, AssignedAt);
        batch.ConfirmPurchase(
            agentId,
            new Dictionary<Guid, (int, decimal)> { [productId] = (2, 10_000m) },
            AssignedAt.AddMinutes(5));

        var purchased = batch.AssignItems(
            new Dictionary<Guid, Guid> { [productId] = Guid.NewGuid() },
            AssignedAt.AddMinutes(10));

        purchased.IsFailure.Should().BeTrue();
        purchased.Error.Code.Should().Be("ITEM_ALREADY_PURCHASED");
    }

    [Fact]
    public void ConfirmPurchase_RequiresExactAgentSubsetAndOnlyUpdatesThatSubset()
    {
        var firstProductId = Guid.NewGuid();
        var secondProductId = Guid.NewGuid();
        var firstAgentId = Guid.NewGuid();
        var secondAgentId = Guid.NewGuid();
        var batch = BuildManifestedBatch(firstProductId, secondProductId);
        batch.AssignItems(
            new Dictionary<Guid, Guid>
            {
                [firstProductId] = firstAgentId,
                [secondProductId] = secondAgentId
            },
            AssignedAt);
        batch.ClearDomainEvents();

        var crossAgent = batch.ConfirmPurchase(
            firstAgentId,
            new Dictionary<Guid, (int, decimal)> { [secondProductId] = (3, 12_000m) },
            AssignedAt.AddMinutes(5));

        crossAgent.IsFailure.Should().BeTrue();
        crossAgent.Error.Code.Should().Be("PURCHASE_LINES_MISMATCH");

        var capturedAt = AssignedAt.AddMinutes(10);
        var result = batch.ConfirmPurchase(
            firstAgentId,
            new Dictionary<Guid, (int, decimal)> { [firstProductId] = (2, 11_000m) },
            capturedAt);

        result.IsSuccess.Should().BeTrue();
        batch.Items.Single(item => item.MarketProductId == firstProductId)
            .PurchasedAt.Should().Be(capturedAt);
        batch.Items.Single(item => item.MarketProductId == firstProductId)
            .UpdatedAt.Should().Be(capturedAt);
        batch.Items.Single(item => item.MarketProductId == secondProductId)
            .PurchasedAt.Should().BeNull();
        batch.Status.Should().Be(ProcurementBatchStatus.Purchasing);

        var batchUpdatedAt = batch.UpdatedAt;
        batch.ConfirmPurchase(
            secondAgentId,
            new Dictionary<Guid, (int, decimal)> { [secondProductId] = (3, 12_000m) },
            capturedAt.AddMinutes(5));
        batch.UpdatedAt.Should().Be(batchUpdatedAt);
    }

    [Fact]
    public void ReportException_RejectsItemOwnedByAnotherAgent()
    {
        var productId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var batch = BuildManifestedBatch(productId);
        batch.AssignItems(new Dictionary<Guid, Guid> { [productId] = ownerId }, AssignedAt);

        var result = batch.ReportException(
            productId,
            ProcurementExceptionType.Unavailable,
            0,
            null,
            null,
            Guid.NewGuid(),
            AssignedAt.AddMinutes(5));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ITEM_NOT_ASSIGNED_TO_AGENT");
        batch.Exceptions.Should().BeEmpty();
    }

    [Fact]
    public void HandoverToHub_RequiresEveryNonExemptItemSettledAndUsesCallingAgent()
    {
        var firstProductId = Guid.NewGuid();
        var secondProductId = Guid.NewGuid();
        var firstAgentId = Guid.NewGuid();
        var secondAgentId = Guid.NewGuid();
        var batch = BuildManifestedBatch(firstProductId, secondProductId);
        batch.AssignItems(
            new Dictionary<Guid, Guid>
            {
                [firstProductId] = firstAgentId,
                [secondProductId] = secondAgentId
            },
            AssignedAt);
        batch.ConfirmPurchase(
            firstAgentId,
            new Dictionary<Guid, (int, decimal)> { [firstProductId] = (2, 11_000m) },
            AssignedAt.AddMinutes(5));

        var incomplete = batch.HandoverToHub(firstAgentId, AssignedAt.AddMinutes(10));

        incomplete.IsFailure.Should().BeTrue();
        incomplete.Error.Code.Should().Be("BATCH_INCOMPLETE");

        batch.ReportException(
            secondProductId,
            ProcurementExceptionType.Unavailable,
            0,
            null,
            null,
            secondAgentId,
            AssignedAt.AddMinutes(15));
        batch.ClearDomainEvents();

        var result = batch.HandoverToHub(firstAgentId, AssignedAt.AddMinutes(20));

        result.IsSuccess.Should().BeTrue();
        batch.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProcurementBatchHandedOffDomainEvent>()
            .Which.HandedOffByUserId.Should().Be(firstAgentId);
    }

    private static ProcurementBatch BuildManifestedBatch(params Guid[] marketProductIds)
    {
        var batch = ProcurementBatch.Build(
            new DateOnly(2026, 8, 12),
            Guid.NewGuid(),
            marketProductIds.Select(id => (id, $"Product {id}", 2, Guid.NewGuid())),
            Guid.NewGuid(),
            "TEST-260812-001").Value;
        batch.Manifest(
            marketProductIds.ToDictionary(id => id, _ => 10_000m),
            ManifestedAt);
        return batch;
    }
}
