using FluentAssertions;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using FreshFlow.Procurement.Domain.Events;

namespace FreshFlow.Procurement.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class ProcurementBatchExceptionTests
{
    private static readonly DateTime ReportedAt =
        new(2026, 7, 15, 3, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void ReportException_ManifestedBatch_AddsExceptionAndRaisesEvent()
    {
        var productId = Guid.NewGuid();
        var agentUserId = Guid.NewGuid();
        var batch = BuildManifestedBatch(productId);
        batch.ClearDomainEvents();

        var result = batch.ReportException(
            productId,
            ProcurementExceptionType.Unavailable,
            0,
            "  Sold out  ",
            "https://res.cloudinary.com/freshflow/image/upload/proof.jpg",
            agentUserId,
            ReportedAt);

        result.IsSuccess.Should().BeTrue();
        batch.Status.Should().Be(ProcurementBatchStatus.Manifested);
        batch.UpdatedAt.Should().Be(ReportedAt);
        var exception = batch.Exceptions.Should().ContainSingle().Subject;
        exception.MarketProductId.Should().Be(productId);
        exception.Type.Should().Be(ProcurementExceptionType.Unavailable);
        exception.ReportedQuantity.Should().Be(0);
        exception.Note.Should().Be("Sold out");
        exception.ProofImageUrl.Should().NotBeNull();
        exception.ReportedByUserId.Should().Be(agentUserId);
        exception.ReportedAt.Should().Be(ReportedAt);
        var domainEvent = batch.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProcurementExceptionReportedDomainEvent>().Subject;
        domainEvent.BatchId.Should().Be(batch.Id);
        domainEvent.ExceptionId.Should().Be(exception.Id);
        domainEvent.MarketProductId.Should().Be(productId);
        domainEvent.Type.Should().Be(ProcurementExceptionType.Unavailable);
    }

    [Fact]
    public void ReportException_PurchasingBatch_AddsException()
    {
        var productId = Guid.NewGuid();
        var batch = BuildManifestedBatch(productId);
        batch.ConfirmPurchase(
            new Dictionary<Guid, (int, decimal)> { [productId] = (2, 10_000m) },
            ReportedAt.AddMinutes(-10));
        batch.ClearDomainEvents();

        var result = batch.ReportException(
            productId,
            ProcurementExceptionType.Damaged,
            1,
            null,
            null,
            Guid.NewGuid(),
            ReportedAt);

        result.IsSuccess.Should().BeTrue();
        batch.Status.Should().Be(ProcurementBatchStatus.Purchasing);
        batch.Exceptions.Should().ContainSingle()
            .Which.Type.Should().Be(ProcurementExceptionType.Damaged);
    }

    [Theory]
    [InlineData(ProcurementBatchStatus.Built)]
    [InlineData(ProcurementBatchStatus.HandedOff)]
    public void ReportException_NonReportableStatus_ReturnsConflict(
        ProcurementBatchStatus status)
    {
        var productId = Guid.NewGuid();
        var batch = status == ProcurementBatchStatus.Built
            ? BuildBatch(productId)
            : BuildManifestedBatch(productId);
        typeof(ProcurementBatch).GetProperty(nameof(ProcurementBatch.Status))!
            .SetValue(batch, status);
        batch.ClearDomainEvents();

        var result = batch.ReportException(
            productId,
            ProcurementExceptionType.Shortfall,
            1,
            null,
            null,
            Guid.NewGuid(),
            ReportedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_NOT_REPORTABLE");
        batch.Exceptions.Should().BeEmpty();
        batch.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ReportException_UnknownProduct_ReturnsValidation()
    {
        var batch = BuildManifestedBatch(Guid.NewGuid());
        batch.ClearDomainEvents();

        var result = batch.ReportException(
            Guid.NewGuid(),
            ProcurementExceptionType.Shortfall,
            1,
            null,
            null,
            Guid.NewGuid(),
            ReportedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PRODUCT_NOT_IN_BATCH");
        batch.Exceptions.Should().BeEmpty();
    }

    [Fact]
    public void ReportException_NegativeQuantity_ReturnsValidation()
    {
        var productId = Guid.NewGuid();
        var batch = BuildManifestedBatch(productId);
        batch.ClearDomainEvents();

        var result = batch.ReportException(
            productId,
            ProcurementExceptionType.PriceDiscrepancy,
            -1,
            null,
            null,
            Guid.NewGuid(),
            ReportedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_EXCEPTION_QUANTITY");
        batch.Exceptions.Should().BeEmpty();
    }

    [Fact]
    public void ConfirmPurchase_UnavailableItemOmitted_SucceedsWithoutActuals()
    {
        var unavailableProductId = Guid.NewGuid();
        var purchasedProductId = Guid.NewGuid();
        var batch = BuildManifestedBatch(unavailableProductId, purchasedProductId);
        batch.ReportException(
            unavailableProductId,
            ProcurementExceptionType.Unavailable,
            2,
            null,
            null,
            Guid.NewGuid(),
            ReportedAt);
        batch.ClearDomainEvents();

        var result = batch.ConfirmPurchase(
            new Dictionary<Guid, (int, decimal)>
            {
                [purchasedProductId] = (2, 11_000m)
            },
            ReportedAt.AddMinutes(5));

        result.IsSuccess.Should().BeTrue();
        batch.Status.Should().Be(ProcurementBatchStatus.Purchasing);
        batch.Items.Single(item => item.MarketProductId == unavailableProductId)
            .ActualQuantity.Should().BeNull();
        batch.Items.Single(item => item.MarketProductId == purchasedProductId)
            .ActualQuantity.Should().Be(2);
    }

    [Fact]
    public void ConfirmPurchase_NonExemptItemOmitted_ReturnsValidation()
    {
        var shortfallProductId = Guid.NewGuid();
        var purchasedProductId = Guid.NewGuid();
        var batch = BuildManifestedBatch(shortfallProductId, purchasedProductId);
        batch.ReportException(
            shortfallProductId,
            ProcurementExceptionType.Shortfall,
            1,
            null,
            null,
            Guid.NewGuid(),
            ReportedAt);

        var result = batch.ConfirmPurchase(
            new Dictionary<Guid, (int, decimal)>
            {
                [purchasedProductId] = (2, 11_000m)
            },
            ReportedAt.AddMinutes(5));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PURCHASE_LINES_MISMATCH");
    }

    private static ProcurementBatch BuildManifestedBatch(params Guid[] marketProductIds)
    {
        var batch = BuildBatch(marketProductIds);
        batch.Manifest(
            marketProductIds.ToDictionary(id => id, _ => 10_000m),
            ReportedAt.AddHours(-1));
        return batch;
    }

    private static ProcurementBatch BuildBatch(params Guid[] marketProductIds) =>
        ProcurementBatch.Build(
            new DateOnly(2026, 7, 15),
            Guid.NewGuid(),
            marketProductIds.Select(id => (id, $"Product {id}", 2, Guid.NewGuid())))
        .Value;
}
