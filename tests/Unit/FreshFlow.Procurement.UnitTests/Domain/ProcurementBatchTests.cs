using FluentAssertions;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.Procurement.Domain.Events;

namespace FreshFlow.Procurement.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class ProcurementBatchTests
{
    private static readonly Guid AgentUserId = Guid.NewGuid();

    [Fact]
    public void Build_WithCode_AssignsCodeOnceAndMergeKeepsIt()
    {
        var productId = Guid.NewGuid();
        var batch = ProcurementBatch.Build(
            new DateOnly(2026, 8, 12),
            Guid.NewGuid(),
            [(productId, "Cabbage", 4, Guid.NewGuid())],
            Guid.NewGuid(),
            "TD-260812-1").Value;

        batch.MergeIn(
            [(productId, "Cabbage", 2, Guid.NewGuid())],
            new Dictionary<Guid, decimal>(),
            DateTime.UtcNow);

        batch.Code.Should().Be("TD-260812-1");
    }

    [Fact]
    public void Build_LegacyOverload_LeavesCodeNull()
    {
        var batch = ProcurementBatch.Build(
            new DateOnly(2026, 8, 12),
            Guid.NewGuid(),
            [(Guid.NewGuid(), "Cabbage", 4, Guid.NewGuid())],
            Guid.NewGuid()).Value;

        batch.Code.Should().BeNull();
    }

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
    public void MergeIn_BuiltBatch_AccumulatesItemsAndRaisesEventOnlyForNewOrders()
    {
        var marketId = Guid.NewGuid();
        var existingProductId = Guid.NewGuid();
        var newProductId = Guid.NewGuid();
        var existingOrderId = Guid.NewGuid();
        var newOrderId = Guid.NewGuid();
        var batch = ProcurementBatch.Build(
            new DateOnly(2026, 7, 15),
            marketId,
            [(existingProductId, "Cabbage", 4, existingOrderId)],
            Guid.NewGuid()).Value;
        var capturedAt = new DateTime(2026, 7, 15, 2, 0, 0, DateTimeKind.Utc);
        batch.ClearDomainEvents();

        var result = batch.MergeIn(
            [
                (existingProductId, "Cabbage", 3, newOrderId),
                (newProductId, "Fish", 2, newOrderId)
            ],
            new Dictionary<Guid, decimal>(),
            capturedAt);

        result.IsSuccess.Should().BeTrue();
        batch.TotalItemCount.Should().Be(2);
        batch.UpdatedAt.Should().Be(capturedAt);
        batch.Items.Should().Contain(item =>
            item.MarketProductId == existingProductId && item.TotalQuantity == 7);
        batch.Items.Should().Contain(item =>
            item.MarketProductId == newProductId && item.TotalQuantity == 2);
        batch.Orders.Select(order => order.OrderId)
            .Should().BeEquivalentTo([existingOrderId, newOrderId]);
        var domainEvent = batch.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProcurementBatchBuiltDomainEvent>().Subject;
        domainEvent.CoveredOrderIds.Should().Equal(newOrderId);
    }

    [Fact]
    public void MergeIn_ManifestedBatch_PricesOnlyNewItems()
    {
        var existingProductId = Guid.NewGuid();
        var newProductId = Guid.NewGuid();
        var batch = BuildBatch(existingProductId);
        batch.Manifest(
            new Dictionary<Guid, decimal> { [existingProductId] = 10_000m },
            new DateTime(2026, 7, 15, 1, 0, 0, DateTimeKind.Utc));
        batch.ClearDomainEvents();

        var result = batch.MergeIn(
            [
                (existingProductId, "Existing", 3, Guid.NewGuid()),
                (newProductId, "New", 4, Guid.NewGuid())
            ],
            new Dictionary<Guid, decimal> { [newProductId] = 12_000m },
            new DateTime(2026, 7, 15, 2, 0, 0, DateTimeKind.Utc));

        result.IsSuccess.Should().BeTrue();
        batch.Items.Should().Contain(item =>
            item.MarketProductId == existingProductId &&
            item.TotalQuantity == 5 &&
            item.ReferenceUnitPrice == 10_000m);
        batch.Items.Should().Contain(item =>
            item.MarketProductId == newProductId &&
            item.TotalQuantity == 4 &&
            item.ReferenceUnitPrice == 12_000m);
    }

    [Fact]
    public void MergeIn_ManifestedBatchMissingNewItemPrice_ReturnsWithoutMutation()
    {
        var existingProductId = Guid.NewGuid();
        var newProductId = Guid.NewGuid();
        var batch = BuildManifestedBatch(existingProductId);
        batch.ClearDomainEvents();

        var result = batch.MergeIn(
            [(newProductId, "New", 4, Guid.NewGuid())],
            new Dictionary<Guid, decimal>(),
            DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("REFERENCE_PRICE_MISSING");
        batch.Items.Should().ContainSingle();
        batch.Orders.Should().ContainSingle();
        batch.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void MergeIn_ExistingOrder_DoesNotDuplicateLinkOrRaiseEvent()
    {
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var batch = ProcurementBatch.Build(
            new DateOnly(2026, 7, 15),
            Guid.NewGuid(),
            [(productId, "Tomato", 2, orderId)],
            Guid.NewGuid()).Value;
        batch.ClearDomainEvents();

        var result = batch.MergeIn(
            [(productId, "Tomato", 1, orderId)],
            new Dictionary<Guid, decimal>(),
            DateTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        batch.Orders.Should().ContainSingle().Which.OrderId.Should().Be(orderId);
        batch.Items.Should().ContainSingle().Which.TotalQuantity.Should().Be(2);
        batch.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ProcurementBatchStatus.Purchasing)]
    [InlineData(ProcurementBatchStatus.HandedOff)]
    [InlineData(ProcurementBatchStatus.Cancelled)]
    public void MergeIn_NonMergeableStatus_ReturnsConflict(ProcurementBatchStatus status)
    {
        var batch = BuildBatch(Guid.NewGuid());
        typeof(ProcurementBatch).GetProperty(nameof(ProcurementBatch.Status))!
            .SetValue(batch, status);
        batch.ClearDomainEvents();

        var result = batch.MergeIn(
            [(Guid.NewGuid(), "Tomato", 1, Guid.NewGuid())],
            new Dictionary<Guid, decimal>(),
            DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_NOT_MERGEABLE");
        batch.DomainEvents.Should().BeEmpty();
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
    public void AssignItems_BuiltBatch_ReturnsNotManifestedConflict()
    {
        var productId = Guid.NewGuid();
        var batch = BuildBatch(productId);
        batch.ClearDomainEvents();

        var result = batch.AssignItems(
            new Dictionary<Guid, Guid> { [productId] = AgentUserId },
            DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_NOT_MANIFESTED");
        batch.Items.Should().OnlyContain(item => item.AssignedAgentUserId == null);
        batch.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AssignItems_PurchasingBatch_AllowsUnpurchasedItem()
    {
        var productId = Guid.NewGuid();
        var batch = BuildManifestedBatch(productId);
        typeof(ProcurementBatch).GetProperty(nameof(ProcurementBatch.Status))!
            .SetValue(batch, ProcurementBatchStatus.Purchasing);
        batch.ClearDomainEvents();

        var newAgentId = Guid.NewGuid();
        var result = batch.AssignItems(
            new Dictionary<Guid, Guid> { [productId] = newAgentId },
            DateTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        batch.Items.Should().ContainSingle()
            .Which.AssignedAgentUserId.Should().Be(newAgentId);
    }

    [Fact]
    public void AssignItems_HandedOffBatch_ReturnsConflict()
    {
        var productId = Guid.NewGuid();
        var batch = BuildManifestedBatch(productId);
        typeof(ProcurementBatch).GetProperty(nameof(ProcurementBatch.Status))!
            .SetValue(batch, ProcurementBatchStatus.HandedOff);
        batch.ClearDomainEvents();

        var result = batch.AssignItems(
            new Dictionary<Guid, Guid> { [productId] = Guid.NewGuid() },
            DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_ALREADY_IN_PROGRESS");
        batch.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ConfirmPurchase_ManifestedBatch_SetsActualsTransitionsAndRaisesEvent()
    {
        var firstProductId = Guid.NewGuid();
        var secondProductId = Guid.NewGuid();
        var batch = BuildManifestedBatch(firstProductId, secondProductId);
        var capturedAt = new DateTime(2026, 7, 15, 3, 0, 0, DateTimeKind.Utc);
        batch.ClearDomainEvents();

        var result = batch.ConfirmPurchase(
            AgentUserId,
            new Dictionary<Guid, (int, decimal)>
            {
                [firstProductId] = (4, 12_000m),
                [secondProductId] = (7, 8_500m)
            },
            capturedAt);

        result.IsSuccess.Should().BeTrue();
        batch.Status.Should().Be(ProcurementBatchStatus.Purchasing);
        batch.UpdatedAt.Should().Be(capturedAt);
        batch.Items.Should().Contain(item =>
            item.MarketProductId == firstProductId &&
            item.ActualQuantity == 4 &&
            item.ActualUnitPrice == 12_000m &&
            item.PurchasedAt == capturedAt);
        batch.Items.Should().Contain(item =>
            item.MarketProductId == secondProductId &&
            item.ActualQuantity == 7 &&
            item.ActualUnitPrice == 8_500m &&
            item.PurchasedAt == capturedAt);
        var domainEvent = batch.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProcurementPurchaseConfirmedDomainEvent>().Subject;
        domainEvent.BatchId.Should().Be(batch.Id);
        domainEvent.MarketId.Should().Be(batch.MarketId);
        domainEvent.ConfirmedAt.Should().Be(capturedAt);
    }

    [Fact]
    public void ConfirmPurchase_PurchasingBatch_OverwritesActuals()
    {
        var productId = Guid.NewGuid();
        var batch = BuildManifestedBatch(productId);
        var firstCapture = new DateTime(2026, 7, 15, 3, 0, 0, DateTimeKind.Utc);
        var secondCapture = firstCapture.AddMinutes(10);
        batch.ConfirmPurchase(
            AgentUserId,
            new Dictionary<Guid, (int, decimal)> { [productId] = (2, 10_000m) },
            firstCapture);
        batch.ClearDomainEvents();

        var result = batch.ConfirmPurchase(
            AgentUserId,
            new Dictionary<Guid, (int, decimal)> { [productId] = (3, 11_000m) },
            secondCapture);

        result.IsSuccess.Should().BeTrue();
        batch.Status.Should().Be(ProcurementBatchStatus.Purchasing);
        var item = batch.Items.Should().ContainSingle().Subject;
        item.ActualQuantity.Should().Be(3);
        item.ActualUnitPrice.Should().Be(11_000m);
        item.PurchasedAt.Should().Be(secondCapture);
        batch.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProcurementPurchaseConfirmedDomainEvent>();
    }

    [Fact]
    public void ConfirmPurchase_BuiltBatch_ReturnsNotManifestedConflict()
    {
        var productId = Guid.NewGuid();
        var batch = BuildBatch(productId);
        batch.ClearDomainEvents();

        var result = batch.ConfirmPurchase(
            AgentUserId,
            new Dictionary<Guid, (int, decimal)> { [productId] = (2, 10_000m) },
            DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_NOT_MANIFESTED");
        batch.Status.Should().Be(ProcurementBatchStatus.Built);
        batch.Items.Should().OnlyContain(item => item.ActualQuantity == null);
        batch.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ConfirmPurchase_HandedOffBatch_ReturnsConflict()
    {
        var productId = Guid.NewGuid();
        var batch = BuildManifestedBatch(productId);
        typeof(ProcurementBatch).GetProperty(nameof(ProcurementBatch.Status))!
            .SetValue(batch, ProcurementBatchStatus.HandedOff);
        batch.ClearDomainEvents();

        var result = batch.ConfirmPurchase(
            AgentUserId,
            new Dictionary<Guid, (int, decimal)> { [productId] = (2, 10_000m) },
            DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_ALREADY_HANDED_OFF");
        batch.Items.Should().OnlyContain(item => item.ActualQuantity == null);
        batch.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ConfirmPurchase_MissingLine_ReturnsValidation()
    {
        var firstProductId = Guid.NewGuid();
        var batch = BuildManifestedBatch(firstProductId, Guid.NewGuid());
        batch.ClearDomainEvents();

        var result = batch.ConfirmPurchase(
            AgentUserId,
            new Dictionary<Guid, (int, decimal)> { [firstProductId] = (2, 10_000m) },
            DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PURCHASE_LINES_MISMATCH");
        batch.Items.Should().OnlyContain(item => item.ActualQuantity == null);
    }

    [Fact]
    public void ConfirmPurchase_ExtraLine_ReturnsValidation()
    {
        var productId = Guid.NewGuid();
        var batch = BuildManifestedBatch(productId);
        batch.ClearDomainEvents();

        var result = batch.ConfirmPurchase(
            AgentUserId,
            new Dictionary<Guid, (int, decimal)>
            {
                [productId] = (2, 10_000m),
                [Guid.NewGuid()] = (1, 5_000m)
            },
            DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PURCHASE_LINES_MISMATCH");
        batch.Items.Should().OnlyContain(item => item.ActualQuantity == null);
    }

    [Theory]
    [InlineData(0, 10_000)]
    [InlineData(-1, 10_000)]
    [InlineData(2, 0)]
    [InlineData(2, -1)]
    public void ConfirmPurchase_InvalidValues_ReturnsValidation(
        int actualQuantity,
        decimal actualUnitPrice)
    {
        var productId = Guid.NewGuid();
        var batch = BuildManifestedBatch(productId);
        batch.ClearDomainEvents();

        var result = batch.ConfirmPurchase(
            AgentUserId,
            new Dictionary<Guid, (int, decimal)>
            {
                [productId] = (actualQuantity, actualUnitPrice)
            },
            DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_PURCHASE_LINE");
        batch.Items.Should().OnlyContain(item => item.ActualQuantity == null);
    }

    [Fact]
    public void HandoverToHub_PurchasingBatch_SetsTraceabilityAndRaisesEvent()
    {
        var batch = BuildPurchasingBatch(Guid.NewGuid(), Guid.NewGuid());
        var hubId = batch.HubId;
        var handedOffAt = new DateTime(2026, 7, 15, 4, 0, 0, DateTimeKind.Utc);
        var coveredOrderIds = batch.Orders.Select(order => order.OrderId).ToArray();
        batch.ClearDomainEvents();

        var result = batch.HandoverToHub(AgentUserId, handedOffAt);

        result.IsSuccess.Should().BeTrue();
        batch.Status.Should().Be(ProcurementBatchStatus.HandedOff);
        batch.HandedOffAt.Should().Be(handedOffAt);
        batch.HubId.Should().Be(hubId);
        batch.UpdatedAt.Should().Be(handedOffAt);
        var domainEvent = batch.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProcurementBatchHandedOffDomainEvent>().Subject;
        domainEvent.BatchId.Should().Be(batch.Id);
        domainEvent.MarketId.Should().Be(batch.MarketId);
        domainEvent.HubId.Should().Be(hubId);
        domainEvent.HandedOffAt.Should().Be(handedOffAt);
        domainEvent.CoveredOrderIds.Should().BeEquivalentTo(coveredOrderIds);
        domainEvent.PurchasedLines.Should().BeEquivalentTo(batch.Items.Select(item =>
            new ProcurementPurchasedLine(
                item.MarketProductId,
                item.ActualQuantity!.Value,
                item.ActualUnitPrice)));
    }

    [Theory]
    [InlineData(ProcurementBatchStatus.Built)]
    [InlineData(ProcurementBatchStatus.Manifested)]
    public void HandoverToHub_NotPurchased_ReturnsConflict(ProcurementBatchStatus status)
    {
        var productId = Guid.NewGuid();
        var batch = status == ProcurementBatchStatus.Built
            ? BuildBatch(productId)
            : BuildManifestedBatch(productId);
        batch.ClearDomainEvents();

        var result = batch.HandoverToHub(AgentUserId, DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_NOT_PURCHASED");
        batch.Status.Should().Be(status);
        batch.HandedOffAt.Should().BeNull();
        batch.HubId.Should().NotBeNull();
        batch.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void HandoverToHub_HandedOffBatch_ReturnsConflict()
    {
        var batch = BuildPurchasingBatch(Guid.NewGuid());
        var firstHubId = batch.HubId;
        var firstHandover = new DateTime(2026, 7, 15, 4, 0, 0, DateTimeKind.Utc);
        batch.HandoverToHub(AgentUserId, firstHandover);
        batch.ClearDomainEvents();

        var result = batch.HandoverToHub(AgentUserId, firstHandover.AddMinutes(5));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_ALREADY_HANDED_OFF");
        batch.Status.Should().Be(ProcurementBatchStatus.HandedOff);
        batch.HandedOffAt.Should().Be(firstHandover);
        batch.HubId.Should().Be(firstHubId);
        batch.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ProcurementBatchStatus.Built)]
    [InlineData(ProcurementBatchStatus.Manifested)]
    public void Cancel_BeforePurchase_CancelsSessionAndRaisesEventCoveringEveryOrder(
        ProcurementBatchStatus status)
    {
        var productId = Guid.NewGuid();
        var batch = status == ProcurementBatchStatus.Built
            ? BuildBatch(productId, Guid.NewGuid())
            : BuildManifestedBatch(productId, Guid.NewGuid());
        var cancelledAt = new DateTime(2026, 7, 15, 2, 0, 0, DateTimeKind.Utc);
        var coveredOrderIds = batch.Orders.Select(order => order.OrderId).ToArray();
        batch.ClearDomainEvents();

        var result = batch.Cancel("Market closed unexpectedly", cancelledAt);

        result.IsSuccess.Should().BeTrue();
        batch.Status.Should().Be(ProcurementBatchStatus.Cancelled);
        batch.CancelledAt.Should().Be(cancelledAt);
        batch.CancellationReason.Should().Be("Market closed unexpectedly");
        batch.UpdatedAt.Should().Be(cancelledAt);
        var domainEvent = batch.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProcurementBatchCancelledDomainEvent>().Subject;
        domainEvent.BatchId.Should().Be(batch.Id);
        domainEvent.MarketId.Should().Be(batch.MarketId);
        domainEvent.Reason.Should().Be("Market closed unexpectedly");
        domainEvent.CancelledAt.Should().Be(cancelledAt);
        domainEvent.CoveredOrderIds.Should().BeEquivalentTo(coveredOrderIds);
    }

    [Fact]
    public void Cancel_PurchasedBatch_ReturnsConflict()
    {
        var batch = BuildPurchasingBatch(Guid.NewGuid());
        batch.ClearDomainEvents();

        var result = batch.Cancel("Too late", DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_NOT_CANCELLABLE");
        batch.Status.Should().Be(ProcurementBatchStatus.Purchasing);
        batch.CancelledAt.Should().BeNull();
        batch.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Cancel_HandedOffBatch_ReturnsConflict()
    {
        var batch = BuildPurchasingBatch(Guid.NewGuid());
        batch.HandoverToHub(AgentUserId, new DateTime(2026, 7, 15, 4, 0, 0, DateTimeKind.Utc));
        batch.ClearDomainEvents();

        var result = batch.Cancel("Too late", DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_NOT_CANCELLABLE");
        batch.Status.Should().Be(ProcurementBatchStatus.HandedOff);
        batch.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AssignItems_CancelledBatch_ReturnsConflict()
    {
        var productId = Guid.NewGuid();
        var batch = BuildManifestedBatch(productId);
        batch.Cancel("Market closed", new DateTime(2026, 7, 15, 2, 0, 0, DateTimeKind.Utc));

        var result = batch.AssignItems(
            new Dictionary<Guid, Guid> { [productId] = Guid.NewGuid() },
            DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_CANCELLED");
    }

    [Fact]
    public void ConfirmPurchase_CancelledBatch_ReturnsConflict()
    {
        var productId = Guid.NewGuid();
        var batch = BuildManifestedBatch(productId);
        batch.Cancel("Market closed", new DateTime(2026, 7, 15, 2, 0, 0, DateTimeKind.Utc));

        var result = batch.ConfirmPurchase(
            AgentUserId,
            new Dictionary<Guid, (int ActualQuantity, decimal ActualUnitPrice)>
            {
                [productId] = (2, 10_000m)
            },
            DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_CANCELLED");
        batch.Status.Should().Be(ProcurementBatchStatus.Cancelled);
    }

    [Fact]
    public void MarkCompleted_HandedOffBatch_SetsCompletedAtAndRaisesEvent()
    {
        var batch = BuildPurchasingBatch(Guid.NewGuid());
        batch.HandoverToHub(AgentUserId, new DateTime(2026, 7, 15, 4, 0, 0, DateTimeKind.Utc));
        batch.ClearDomainEvents();
        var completedAt = new DateTime(2026, 7, 16, 9, 0, 0, DateTimeKind.Utc);

        var result = batch.MarkCompleted(completedAt);

        result.IsSuccess.Should().BeTrue();
        batch.Status.Should().Be(ProcurementBatchStatus.Completed);
        batch.CompletedAt.Should().Be(completedAt);
        batch.UpdatedAt.Should().Be(completedAt);
        var domainEvent = batch.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProcurementBatchCompletedDomainEvent>().Subject;
        domainEvent.BatchId.Should().Be(batch.Id);
        domainEvent.MarketId.Should().Be(batch.MarketId);
        domainEvent.CompletedAt.Should().Be(completedAt);
    }

    [Theory]
    [InlineData(ProcurementBatchStatus.Built)]
    [InlineData(ProcurementBatchStatus.Manifested)]
    [InlineData(ProcurementBatchStatus.Purchasing)]
    [InlineData(ProcurementBatchStatus.Cancelled)]
    public void MarkCompleted_NotHandedOff_ReturnsConflict(ProcurementBatchStatus status)
    {
        var batch = BuildBatch(Guid.NewGuid());
        typeof(ProcurementBatch).GetProperty(nameof(ProcurementBatch.Status))!
            .SetValue(batch, status);
        batch.ClearDomainEvents();

        var result = batch.MarkCompleted(DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_NOT_COMPLETABLE");
        batch.Status.Should().Be(status);
        batch.CompletedAt.Should().BeNull();
        batch.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void MarkCompleted_AlreadyCompleted_IsIdempotentWithNoSecondEvent()
    {
        var batch = BuildPurchasingBatch(Guid.NewGuid());
        batch.HandoverToHub(AgentUserId, new DateTime(2026, 7, 15, 4, 0, 0, DateTimeKind.Utc));
        var firstCompletion = new DateTime(2026, 7, 16, 9, 0, 0, DateTimeKind.Utc);
        batch.MarkCompleted(firstCompletion);
        batch.ClearDomainEvents();

        var result = batch.MarkCompleted(firstCompletion.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        batch.Status.Should().Be(ProcurementBatchStatus.Completed);
        batch.CompletedAt.Should().Be(firstCompletion);
        batch.DomainEvents.Should().BeEmpty();
    }

    private static ProcurementBatch BuildManifestedBatch(params Guid[] marketProductIds)
    {
        var batch = BuildBatch(marketProductIds);
        batch.Manifest(
            marketProductIds.ToDictionary(id => id, _ => 10_000m),
            new DateTime(2026, 7, 15, 1, 0, 0, DateTimeKind.Utc));
        batch.AssignItems(
            marketProductIds.ToDictionary(id => id, _ => AgentUserId),
            new DateTime(2026, 7, 15, 2, 0, 0, DateTimeKind.Utc));
        return batch;
    }

    private static ProcurementBatch BuildPurchasingBatch(params Guid[] marketProductIds)
    {
        var batch = BuildManifestedBatch(marketProductIds);
        batch.ConfirmPurchase(
            AgentUserId,
            marketProductIds.ToDictionary(
                id => id,
                _ => (ActualQuantity: 2, ActualUnitPrice: 10_000m)),
            new DateTime(2026, 7, 15, 3, 0, 0, DateTimeKind.Utc));
        return batch;
    }

    private static ProcurementBatch BuildBatch(params Guid[] marketProductIds) =>
        ProcurementBatch.Build(
            new DateOnly(2026, 7, 15),
            Guid.NewGuid(),
            marketProductIds.Select(id =>
                (id, $"Product {id}", 2, Guid.NewGuid())),
            Guid.NewGuid())
        .Value;
}
