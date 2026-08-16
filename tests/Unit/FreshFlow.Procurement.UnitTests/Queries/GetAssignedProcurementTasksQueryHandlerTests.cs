using FluentAssertions;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Queries.GetAssignedProcurementTasks;
using FreshFlow.Procurement.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetAssignedProcurementTasksQueryHandlerTests
{
    [Fact]
    public void Validator_InvalidScopeAndPagination_IsInvalid()
    {
        var result = new GetAssignedProcurementTasksQueryValidator()
            .Validate(new GetAssignedProcurementTasksQuery(Guid.Empty, 0, 101));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_ReturnsAssignedManifestAndPaginationAsync()
    {
        var agentUserId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var batch = BuildAssignedBatch(agentUserId, orderId, productId);
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.ListByAgentAsync(agentUserId, 2, 10, default)
            .Returns((
                (IReadOnlyList<ProcurementBatch>)[batch],
                11));
        var orders = Substitute.For<IConfirmedOrderReader>();
        orders.ReadStatusesAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.SequenceEqual(new[] { orderId })),
                default)
            .Returns(new Dictionary<Guid, string> { [orderId] = "Batched" });
        orders.ReadRestaurantNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
            .Returns(new Dictionary<Guid, string> { [orderId] = "Nhà hàng Phở Thìn" });
        var images = Substitute.For<IMarketProductImageReader>();
        images.ReadInfoAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { productId })),
                default)
            .Returns(new Dictionary<Guid, MarketProductInfoDto>
            {
                [productId] = new("https://img/tomato.jpg", "BOX-15KG", 15m)
            });
        var handler = new GetAssignedProcurementTasksQueryHandler(repository, orders, images);

        var result = await handler.Handle(
            new GetAssignedProcurementTasksQuery(agentUserId, 2, 10),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Batches.Should().ContainSingle();
        result.Value.Batches[0].Items.Should().ContainSingle()
            .Which.AssignedAgentUserId.Should().Be(agentUserId);
        result.Value.Batches[0].Items.Should().ContainSingle()
            .Which.ReferenceUnitPrice.Should().Be(10_000m);
        result.Value.Batches[0].Items[0].ProductImageUrl.Should().Be("https://img/tomato.jpg");
        result.Value.Batches[0].Items[0].PackingCode.Should().Be("BOX-15KG");
        result.Value.Batches[0].Items[0].PackingCapacityKg.Should().Be(15m);
        result.Value.Batches[0].Members.Should().ContainSingle(member =>
            member.Status == "Batched" &&
            member.RestaurantName == "Nhà hàng Phở Thìn");
        result.Value.Pagination.Should().BeEquivalentTo(new
        {
            Total = 11,
            Page = 2,
            PageSize = 10
        });
        await repository.Received(1).ListByAgentAsync(
            agentUserId,
            2,
            10,
            default);
        await repository.DidNotReceiveWithAnyArgs()
            .ListAsync(default, default, default, default, default);
    }

    [Fact]
    public async Task Handle_EmptyPage_ReturnsEmptyResultAsync()
    {
        var agentUserId = Guid.NewGuid();
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.ListByAgentAsync(agentUserId, 1, 20, default)
            .Returns((
                (IReadOnlyList<ProcurementBatch>)Array.Empty<ProcurementBatch>(),
                0));
        var orders = Substitute.For<IConfirmedOrderReader>();
        orders.ReadStatusesAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 0),
                default)
            .Returns(new Dictionary<Guid, string>());
        var images = Substitute.For<IMarketProductImageReader>();
        var handler = new GetAssignedProcurementTasksQueryHandler(repository, orders, images);

        var result = await handler.Handle(
            new GetAssignedProcurementTasksQuery(agentUserId),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Batches.Should().BeEmpty();
        result.Value.Pagination.Should().BeEquivalentTo(new
        {
            Total = 0,
            Page = 1,
            PageSize = 20
        });
    }

    private static ProcurementBatch BuildAssignedBatch(
        Guid agentUserId,
        Guid orderId,
        Guid productId)
    {
        var batch = ProcurementBatch.Build(
            new DateOnly(2026, 7, 16),
            Guid.NewGuid(),
            [(productId, "Tomato", 5, orderId)])
            .Value;
        batch.Manifest(
            new Dictionary<Guid, decimal> { [productId] = 10_000m },
            new DateTime(2026, 7, 15, 1, 0, 0, DateTimeKind.Utc));
        batch.AssignItems(
            new Dictionary<Guid, Guid> { [productId] = agentUserId },
            new DateTime(2026, 7, 15, 2, 0, 0, DateTimeKind.Utc));
        return batch;
    }
}
