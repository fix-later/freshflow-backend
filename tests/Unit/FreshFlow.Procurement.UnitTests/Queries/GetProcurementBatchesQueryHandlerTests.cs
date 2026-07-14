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
        var build = ProcurementBatch.Build(
            new DateOnly(2026, 7, 15),
            Guid.NewGuid(),
            [(Guid.NewGuid(), "Tomato", 4, orderId)]);
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.ListAsync(2, 10, default)
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
}
