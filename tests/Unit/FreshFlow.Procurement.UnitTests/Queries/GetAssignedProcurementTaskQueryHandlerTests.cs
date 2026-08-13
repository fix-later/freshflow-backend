using FluentAssertions;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Queries.GetAssignedProcurementTask;
using FreshFlow.Procurement.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.Queries;

[Trait("Category", "Unit")]
public sealed class GetAssignedProcurementTaskQueryHandlerTests
{
    [Fact]
    public void Validator_EmptyIds_IsInvalid()
    {
        var result = new GetAssignedProcurementTaskQueryValidator()
            .Validate(new GetAssignedProcurementTaskQuery(Guid.Empty, Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_Owner_ReturnsFullManifestAsync()
    {
        var agentUserId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var batch = BuildAssignedBatch(agentUserId, orderId, productId);
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        var orders = Substitute.For<IConfirmedOrderReader>();
        orders.ReadStatusesAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.SequenceEqual(new[] { orderId })),
                default)
            .Returns(new Dictionary<Guid, string> { [orderId] = "Batched" });
        var images = Substitute.For<IMarketProductImageReader>();
        images.ReadImagesAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { productId })),
                default)
            .Returns(new Dictionary<Guid, string> { [productId] = "https://img/tomato.jpg" });
        var handler = new GetAssignedProcurementTaskQueryHandler(repository, orders, images);

        var result = await handler.Handle(
            new GetAssignedProcurementTaskQuery(agentUserId, batch.Id),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(batch.Id);
        result.Value.Status.Should().Be("Manifested");
        result.Value.Items.Should().ContainSingle()
            .Which.AssignedAgentUserId.Should().Be(agentUserId);
        result.Value.Items.Should().ContainSingle(item =>
            item.MarketProductId == productId &&
            item.ReferenceUnitPrice == 10_000m);
        result.Value.Members.Should().ContainSingle(member =>
            member.OrderId == orderId &&
            member.Status == "Batched");
        result.Value.Items.Should().ContainSingle(item =>
            item.ProductImageUrl == "https://img/tomato.jpg");
    }

    [Fact]
    public async Task Handle_OtherAgentsBatch_ReturnsNotFoundAsync()
    {
        var ownerUserId = Guid.NewGuid();
        var batch = BuildAssignedBatch(
            ownerUserId,
            Guid.NewGuid(),
            Guid.NewGuid());
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        var orders = Substitute.For<IConfirmedOrderReader>();
        var images = Substitute.For<IMarketProductImageReader>();
        var handler = new GetAssignedProcurementTaskQueryHandler(repository, orders, images);

        var result = await handler.Handle(
            new GetAssignedProcurementTaskQuery(Guid.NewGuid(), batch.Id),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PROCUREMENT_BATCH_NOT_FOUND");
        await orders.DidNotReceiveWithAnyArgs()
            .ReadStatusesAsync(default!, default);
    }

    [Fact]
    public async Task Handle_UnknownBatch_ReturnsNotFoundAsync()
    {
        var batchId = Guid.NewGuid();
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batchId, default)
            .Returns((ProcurementBatch?)null);
        var orders = Substitute.For<IConfirmedOrderReader>();
        var images = Substitute.For<IMarketProductImageReader>();
        var handler = new GetAssignedProcurementTaskQueryHandler(repository, orders, images);

        var result = await handler.Handle(
            new GetAssignedProcurementTaskQuery(Guid.NewGuid(), batchId),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PROCUREMENT_BATCH_NOT_FOUND");
        await orders.DidNotReceiveWithAnyArgs()
            .ReadStatusesAsync(default!, default);
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
