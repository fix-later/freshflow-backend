using FluentAssertions;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Queries.GetProcurementProgress;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetProcurementProgressQueryHandlerTests
{
    private static readonly DateOnly CycleDate = new(2026, 7, 15);
    private static readonly DateTime CapturedAt =
        new(2026, 7, 14, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_MixedStatuses_ReturnsWholeCycleSummaryAsync()
    {
        var batches = new[]
        {
            BuildBatch(ProcurementBatchStatus.Built),
            BuildBatch(ProcurementBatchStatus.Manifested),
            BuildBatch(ProcurementBatchStatus.Purchasing, partialPurchase: true),
            BuildBatch(ProcurementBatchStatus.HandedOff)
        };
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.ListByDateAsync(CycleDate, default)
            .Returns((IReadOnlyList<ProcurementBatch>)batches);
        var handler = new GetProcurementProgressQueryHandler(repository);

        var result = await handler.Handle(
            new GetProcurementProgressQuery(CycleDate, null),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.Should().BeEquivalentTo(new
        {
            BatchDate = (DateOnly?)CycleDate,
            TotalBatches = 4,
            StatusCounts = new Dictionary<string, int>
            {
                ["Built"] = 1,
                ["Manifested"] = 1,
                ["Purchasing"] = 1,
                ["HandedOff"] = 1
            },
            TotalItems = 5,
            ItemsPurchased = 2,
            ItemsPending = 3,
            OpenExceptions = 1
        });
        result.Value.Batches.Should().HaveCount(4);
        result.Value.Batches.Sum(batch => batch.OrderCount).Should().Be(5);
    }

    [Fact]
    public async Task Handle_PartialPurchase_CountsPurchasedAndPendingItemsAsync()
    {
        var batch = BuildBatch(ProcurementBatchStatus.Purchasing, partialPurchase: true);
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.ListByDateAsync(CycleDate, default)
            .Returns((IReadOnlyList<ProcurementBatch>)[batch]);
        var handler = new GetProcurementProgressQueryHandler(repository);

        var result = await handler.Handle(
            new GetProcurementProgressQuery(CycleDate, null),
            default);

        result.Value.Batches.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            ItemsTotal = 2,
            ItemsPurchased = 1,
            ItemsPending = 1,
            ExceptionCount = 1,
            OrderCount = 2
        });
    }

    [Fact]
    public async Task Handle_StatusFilter_FiltersRowsButNotSummaryAsync()
    {
        var batches = new[]
        {
            BuildBatch(ProcurementBatchStatus.Built),
            BuildBatch(ProcurementBatchStatus.Purchasing)
        };
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.ListByDateAsync(CycleDate, default)
            .Returns((IReadOnlyList<ProcurementBatch>)batches);
        var handler = new GetProcurementProgressQueryHandler(repository);

        var result = await handler.Handle(
            new GetProcurementProgressQuery(CycleDate, "purchasing"),
            default);

        result.Value.Summary.TotalBatches.Should().Be(2);
        result.Value.Summary.StatusCounts["Built"].Should().Be(1);
        result.Value.Summary.StatusCounts["Purchasing"].Should().Be(1);
        result.Value.Batches.Should().ContainSingle()
            .Which.Status.Should().Be("Purchasing");
    }

    [Fact]
    public async Task Handle_DateOmitted_UsesLatestCycleAsync()
    {
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.GetLatestCycleDateAsync(default).Returns(CycleDate);
        repository.ListByDateAsync(CycleDate, default)
            .Returns((IReadOnlyList<ProcurementBatch>)[BuildBatch(ProcurementBatchStatus.Built)]);
        var handler = new GetProcurementProgressQueryHandler(repository);

        var result = await handler.Handle(
            new GetProcurementProgressQuery(null, null),
            default);

        result.Value.Summary.BatchDate.Should().Be(CycleDate);
        await repository.Received(1).GetLatestCycleDateAsync(default);
        await repository.Received(1).ListByDateAsync(CycleDate, default);
    }

    [Fact]
    public async Task Handle_NoCycles_ReturnsEmptySummaryAsync()
    {
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.GetLatestCycleDateAsync(default).Returns((DateOnly?)null);
        var handler = new GetProcurementProgressQueryHandler(repository);

        var result = await handler.Handle(
            new GetProcurementProgressQuery(null, null),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.BatchDate.Should().BeNull();
        result.Value.Summary.TotalBatches.Should().Be(0);
        result.Value.Summary.StatusCounts.Values.Should().OnlyContain(count => count == 0);
        result.Value.Summary.TotalItems.Should().Be(0);
        result.Value.Summary.ItemsPurchased.Should().Be(0);
        result.Value.Summary.ItemsPending.Should().Be(0);
        result.Value.Summary.OpenExceptions.Should().Be(0);
        result.Value.Batches.Should().BeEmpty();
        await repository.DidNotReceiveWithAnyArgs()
            .ListByDateAsync(default, default);
    }

    [Fact]
    public async Task Handle_InvalidStatus_ReturnsValidationErrorAsync()
    {
        var repository = Substitute.For<IProcurementBatchRepository>();
        var handler = new GetProcurementProgressQueryHandler(repository);

        var result = await handler.Handle(
            new GetProcurementProgressQuery(CycleDate, "Unknown"),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("INVALID_BATCH_STATUS");
        await repository.DidNotReceiveWithAnyArgs()
            .ListByDateAsync(default, default);
    }

    [Fact]
    public void Validator_InvalidStatus_IsInvalid()
    {
        var validator = new GetProcurementProgressQueryValidator();

        validator.Validate(new GetProcurementProgressQuery(null, "Unknown"))
            .IsValid.Should().BeFalse();
        validator.Validate(new GetProcurementProgressQuery(null, null))
            .IsValid.Should().BeTrue();
        validator.Validate(new GetProcurementProgressQuery(null, "handedoff"))
            .IsValid.Should().BeTrue();
    }

    private static ProcurementBatch BuildBatch(
        ProcurementBatchStatus targetStatus,
        bool partialPurchase = false)
    {
        var firstProductId = Guid.NewGuid();
        var secondProductId = Guid.NewGuid();
        var lines = partialPurchase
            ? new[]
            {
                (firstProductId, "Tomato", 2, Guid.NewGuid()),
                (secondProductId, "Potato", 3, Guid.NewGuid())
            }
            : [(firstProductId, "Tomato", 2, Guid.NewGuid())];
        var batch = ProcurementBatch.Build(CycleDate, Guid.NewGuid(), lines).Value;

        if (targetStatus == ProcurementBatchStatus.Built)
            return batch;

        batch.Manifest(
            batch.Items.ToDictionary(item => item.MarketProductId, _ => 10_000m),
            CapturedAt);
        if (targetStatus == ProcurementBatchStatus.Manifested)
            return batch;

        if (partialPurchase)
        {
            batch.ReportException(
                secondProductId,
                ProcurementExceptionType.Unavailable,
                3,
                null,
                null,
                Guid.NewGuid(),
                CapturedAt.AddMinutes(1));
        }

        batch.ConfirmPurchase(
            new Dictionary<Guid, (int, decimal)>
            {
                [firstProductId] = (2, 9_000m)
            },
            CapturedAt.AddMinutes(2));
        if (targetStatus == ProcurementBatchStatus.Purchasing)
            return batch;

        batch.HandoverToHub(Guid.NewGuid(), CapturedAt.AddMinutes(3));
        return batch;
    }
}
