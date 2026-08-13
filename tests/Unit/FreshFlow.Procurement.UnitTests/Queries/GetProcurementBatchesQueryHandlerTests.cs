using FluentAssertions;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Queries.GetProcurementBatches;
using FreshFlow.Procurement.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetProcurementBatchesQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsItemsMembersStatusesAndPaginationAsync()
    {
        var orderId = Guid.NewGuid();
        var marketSessionId = Guid.NewGuid();
        var build = ProcurementBatch.Build(
            new DateOnly(2026, 7, 15),
            Guid.NewGuid(),
            [(Guid.NewGuid(), "Tomato", 4, orderId)],
            Guid.NewGuid(),
            "TD-260715-1",
            marketSessionId);
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.ListAsync(2, 10, default, default, default)
            .Returns((
                (IReadOnlyList<ProcurementBatch>)[build.Value],
                11));
        var orders = Substitute.For<IConfirmedOrderReader>();
        orders.ReadStatusesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
            .Returns(new Dictionary<Guid, string> { [orderId] = "Batched" });
        var handler = new GetProcurementBatchesQueryHandler(repository, orders);

        var result = await handler.Handle(
            new GetProcurementBatchesQuery(2, 10),
            default);

        result.Value.Batches.Should().ContainSingle();
        result.Value.Batches[0].Code.Should().Be("TD-260715-1");
        result.Value.Batches[0].MarketSessionId.Should().Be(marketSessionId);
        result.Value.Batches[0].Members.Should().ContainSingle()
            .Which.Status.Should().Be("Batched");
        result.Value.Batches[0].Items.Should().ContainSingle()
            .Which.TotalQuantity.Should().Be(4);
        result.Value.Pagination.Should().BeEquivalentTo(new
        {
            Total = 11,
            Page = 2,
            PageSize = 10
        });
    }

    [Fact]
    public async Task Handle_ForwardsDateAndMarketIdToRepositoryAsync()
    {
        var date = new DateOnly(2026, 7, 20);
        var marketId = Guid.NewGuid();
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.ListAsync(1, 20, date, marketId, default)
            .Returns(((IReadOnlyList<ProcurementBatch>)[], 0));
        var orders = Substitute.For<IConfirmedOrderReader>();
        orders.ReadStatusesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
            .Returns(new Dictionary<Guid, string>());
        var handler = new GetProcurementBatchesQueryHandler(repository, orders);

        await handler.Handle(
            new GetProcurementBatchesQuery(1, 20, date, marketId),
            default);

        await repository.Received(1).ListAsync(1, 20, date, marketId, default);
    }
}
