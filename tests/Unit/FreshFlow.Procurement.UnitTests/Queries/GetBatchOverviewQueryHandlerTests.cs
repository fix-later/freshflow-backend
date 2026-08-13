using FluentAssertions;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Queries.GetBatchOverview;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetBatchOverviewQueryHandlerTests
{
    [Fact]
    public async Task Handle_MultiAgentBatch_ReturnsCountsCostsAndOrderStatusesAsync()
    {
        var agentA = Guid.NewGuid();
        var agentB = Guid.NewGuid();
        var orderA = Guid.NewGuid();
        var orderB = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var productC = Guid.NewGuid();
        var batch = ProcurementBatch.Build(
            new DateOnly(2026, 8, 13),
            Guid.NewGuid(),
            [
                (productA, "Tomato", 2, orderA),
                (productB, "Potato", 3, orderA),
                (productC, "Carrot", 4, orderB)
            ],
            Guid.NewGuid(),
            "PB-260813-001").Value;
        var capturedAt = new DateTime(2026, 8, 12, 1, 0, 0, DateTimeKind.Utc);
        batch.Manifest(new Dictionary<Guid, decimal>
        {
            [productA] = 10m,
            [productB] = 20m,
            [productC] = 30m
        }, capturedAt);
        batch.AssignItems(new Dictionary<Guid, Guid>
        {
            [productA] = agentA,
            [productB] = agentA,
            [productC] = agentB
        }, capturedAt.AddMinutes(1));
        batch.ReportException(
            productB,
            ProcurementExceptionType.PriceDiscrepancy,
            3,
            null,
            null,
            agentA,
            capturedAt.AddMinutes(2));
        batch.ConfirmPurchase(agentA, new Dictionary<Guid, (int, decimal)>
        {
            [productA] = (2, 12m),
            [productB] = (3, 18m)
        }, capturedAt.AddMinutes(3));

        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        var orders = Substitute.For<IConfirmedOrderReader>();
        orders.ReadStatusesAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.ToHashSet().SetEquals(new[] { orderA, orderB })),
                default)
            .Returns(new Dictionary<Guid, string> { [orderA] = "Batched" });
        var handler = new GetBatchOverviewQueryHandler(repository, orders);

        var result = await handler.Handle(new GetBatchOverviewQuery(batch.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new
        {
            BatchId = batch.Id,
            Code = "PB-260813-001",
            Status = "Purchasing",
            TotalItemCount = 3,
            ItemsPurchased = 2,
            ItemsPending = 1,
            ExceptionCount = 1
        });
        result.Value.Agents.Should().BeEquivalentTo([
            new
            {
                AgentUserId = agentA,
                ItemsAssigned = 2,
                ItemsPurchased = 2,
                ItemsPending = 0,
                ExceptionsReported = 1,
                ReferenceCostTotal = (decimal?)80m,
                ActualCostTotal = (decimal?)78m,
                VarianceTotal = (decimal?)(-2m)
            },
            new
            {
                AgentUserId = agentB,
                ItemsAssigned = 1,
                ItemsPurchased = 0,
                ItemsPending = 1,
                ExceptionsReported = 0,
                ReferenceCostTotal = (decimal?)120m,
                ActualCostTotal = (decimal?)null,
                VarianceTotal = (decimal?)null
            }
        ]);
        result.Value.Orders.Should().BeEquivalentTo([
            new { OrderId = orderA, Status = (string?)"Batched" },
            new { OrderId = orderB, Status = (string?)null }
        ]);
    }

    [Fact]
    public async Task Handle_UnknownBatch_ReturnsNotFoundWithoutReadingOrdersAsync()
    {
        var batchId = Guid.NewGuid();
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batchId, default).Returns((ProcurementBatch?)null);
        var orders = Substitute.For<IConfirmedOrderReader>();
        var handler = new GetBatchOverviewQueryHandler(repository, orders);

        var result = await handler.Handle(new GetBatchOverviewQuery(batchId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PROCUREMENT_BATCH_NOT_FOUND");
        await orders.DidNotReceiveWithAnyArgs().ReadStatusesAsync(default!, default);
    }

    [Fact]
    public void Validator_EmptyBatchId_IsInvalid() =>
        new GetBatchOverviewQueryValidator()
            .Validate(new GetBatchOverviewQuery(Guid.Empty))
            .IsValid.Should().BeFalse();
}
