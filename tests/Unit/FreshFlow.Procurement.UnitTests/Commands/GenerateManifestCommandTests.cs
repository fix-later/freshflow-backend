using FluentAssertions;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Commands.GenerateManifest;
using FreshFlow.Procurement.Domain.Entities;
using FreshFlow.Procurement.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class GenerateManifestCommandTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 14, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Validator_EmptyBatchId_IsInvalid()
    {
        var result = new GenerateManifestCommandValidator()
            .Validate(new GenerateManifestCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Handler_BuiltBatch_ReadsPricesSavesAndReturnsManifestAsync()
    {
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var batch = BuildBatch(productId, orderId);
        batch.ClearDomainEvents();
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        repository.SaveChangesAsync(default).Returns(true);
        var marketProducts = Substitute.For<IMarketProductMarketReader>();
        marketProducts.ReadReferencePricesAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.SequenceEqual(new[] { productId })),
                default)
            .Returns(new Dictionary<Guid, decimal> { [productId] = 12_500m });
        var orders = Substitute.For<IConfirmedOrderReader>();
        orders.ReadStatusesAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids =>
                    ids.SequenceEqual(new[] { orderId })),
                default)
            .Returns(new Dictionary<Guid, string> { [orderId] = "Batched" });
        var handler = new GenerateManifestCommandHandler(
            repository,
            marketProducts,
            orders,
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new GenerateManifestCommand(batch.Id),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Manifested");
        result.Value.ManifestedAt.Should().Be(Now.UtcDateTime);
        result.Value.Items.Should().ContainSingle()
            .Which.ReferenceUnitPrice.Should().Be(12_500m);
        result.Value.Members.Should().ContainSingle()
            .Which.Status.Should().Be("Batched");
        await repository.Received(1).SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handler_UnknownBatch_ReturnsNotFoundAsync()
    {
        var batchId = Guid.NewGuid();
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batchId, default)
            .Returns((ProcurementBatch?)null);
        var marketProducts = Substitute.For<IMarketProductMarketReader>();
        var handler = new GenerateManifestCommandHandler(
            repository,
            marketProducts,
            Substitute.For<IConfirmedOrderReader>(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new GenerateManifestCommand(batchId),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("PROCUREMENT_BATCH_NOT_FOUND");
        await marketProducts.DidNotReceiveWithAnyArgs()
            .ReadReferencePricesAsync(default!, default);
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handler_MissingPrice_PropagatesDomainErrorAsync()
    {
        var productId = Guid.NewGuid();
        var batch = BuildBatch(productId, Guid.NewGuid());
        batch.ClearDomainEvents();
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        var marketProducts = Substitute.For<IMarketProductMarketReader>();
        marketProducts.ReadReferencePricesAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                default)
            .Returns(new Dictionary<Guid, decimal>());
        var handler = new GenerateManifestCommandHandler(
            repository,
            marketProducts,
            Substitute.For<IConfirmedOrderReader>(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new GenerateManifestCommand(batch.Id),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("REFERENCE_PRICE_MISSING");
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Handler_PurchasingBatch_PropagatesConflictAsync()
    {
        var productId = Guid.NewGuid();
        var batch = BuildBatch(productId, Guid.NewGuid());
        typeof(ProcurementBatch).GetProperty(nameof(ProcurementBatch.Status))!
            .SetValue(batch, ProcurementBatchStatus.Purchasing);
        batch.ClearDomainEvents();
        var repository = Substitute.For<IProcurementBatchRepository>();
        repository.FindByIdAsync(batch.Id, default).Returns(batch);
        var marketProducts = Substitute.For<IMarketProductMarketReader>();
        marketProducts.ReadReferencePricesAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                default)
            .Returns(new Dictionary<Guid, decimal> { [productId] = 12_500m });
        var handler = new GenerateManifestCommandHandler(
            repository,
            marketProducts,
            Substitute.For<IConfirmedOrderReader>(),
            new FixedTimeProvider(Now));

        var result = await handler.Handle(
            new GenerateManifestCommand(batch.Id),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("BATCH_NOT_MANIFESTABLE");
        await repository.DidNotReceive().SaveChangesAsync(default);
    }

    private static ProcurementBatch BuildBatch(Guid productId, Guid orderId) =>
        ProcurementBatch.Build(
            new DateOnly(2026, 7, 15),
            Guid.NewGuid(),
            [(productId, "Tomato", 5, orderId)])
        .Value;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
