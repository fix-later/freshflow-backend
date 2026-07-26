using FluentAssertions;
using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.Procurement.Application.Services;
using FreshFlow.Procurement.Domain.Entities;
using NSubstitute;

namespace FreshFlow.Procurement.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class BatchConfirmedOrdersServiceTests
{
    private readonly IConfirmedOrderReader _orders = Substitute.For<IConfirmedOrderReader>();
    private readonly IMarketProductMarketReader _markets = Substitute.For<IMarketProductMarketReader>();
    private readonly IHubByMarketReader _hubs = Substitute.For<IHubByMarketReader>();
    private readonly IOperationalSettingsReader _settings = Substitute.For<IOperationalSettingsReader>();
    private readonly IProcurementBatchRepository _batches = Substitute.For<IProcurementBatchRepository>();

    public BatchConfirmedOrdersServiceTests()
    {
        _hubs.ReadActiveHubsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
            .Returns(call => ((IReadOnlyCollection<Guid>)call[0])
                .ToDictionary(marketId => marketId, _ => Guid.NewGuid()));
    }

    [Fact]
    public async Task BuildBatches_OneOrderAcrossMarkets_FansOutAsync()
    {
        var orderId = Guid.NewGuid();
        var firstProductId = Guid.NewGuid();
        var secondProductId = Guid.NewGuid();
        var firstMarketId = Guid.NewGuid();
        var secondMarketId = Guid.NewGuid();
        ArrangeEnabled();
        _orders.ReadEligibleAsync(Date, false, default).Returns(
        [
            new ConfirmedOrderDto(
                orderId,
                Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                [
                    new ConfirmedOrderItemDto(firstProductId, "Tomato", 3),
                    new ConfirmedOrderItemDto(secondProductId, "Fish", 2)
                ])
        ]);
        _markets.ReadMarketsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
            .Returns(new Dictionary<Guid, Guid>
            {
                [firstProductId] = firstMarketId,
                [secondProductId] = secondMarketId
            });
        _batches.SaveChangesAsync(default).Returns(true);
        IReadOnlyCollection<ProcurementBatch>? persisted = null;
        _batches.AddRangeAsync(
                Arg.Do<IReadOnlyCollection<ProcurementBatch>>(value => persisted = value),
                default)
            .Returns(Task.CompletedTask);

        var result = await CreateSut().BuildBatchesAsync(Date, false, false, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.BatchesCreated.Should().Be(2);
        result.Value.OrdersBatched.Should().Be(1);
        persisted.Should().HaveCount(2);
        persisted!.Should().OnlyContain(batch =>
            batch.Orders.Select(link => link.OrderId).SequenceEqual(new[] { orderId }));
    }

    [Fact]
    public async Task BuildBatches_BatchingDisabled_ShortCircuitsAsync()
    {
        _settings.ReadAsync(default)
            .Returns(new ProcurementOperationalSettingsDto(false, new TimeOnly(22, 0)));

        var result = await CreateSut().BuildBatchesAsync(Date, false, false, default);

        result.Value.Skipped.Should().BeTrue();
        result.Value.Reason.Should().Be("batching_disabled");
        await _orders.DidNotReceiveWithAnyArgs()
            .ReadEligibleAsync(default, default, default);
        await _batches.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(default);
    }

    [Fact]
    public async Task BuildBatches_NoEligibleOrders_IsIdempotentNoOpAsync()
    {
        ArrangeEnabled();
        _orders.ReadEligibleAsync(Date, false, default)
            .Returns(Array.Empty<ConfirmedOrderDto>());

        var first = await CreateSut().BuildBatchesAsync(Date, false, false, default);
        var second = await CreateSut().BuildBatchesAsync(Date, false, false, default);

        first.Value.Skipped.Should().BeTrue();
        second.Value.BatchesCreated.Should().Be(0);
        await _batches.DidNotReceiveWithAnyArgs()
            .AddRangeAsync(default!, default);
    }

    [Fact]
    public async Task BuildBatches_DryRun_ReturnsPreviewWithoutWritesAsync()
    {
        var productId = Guid.NewGuid();
        ArrangeSingleOrder(productId);
        _markets.ReadMarketsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
            .Returns(new Dictionary<Guid, Guid> { [productId] = Guid.NewGuid() });

        var result = await CreateSut().BuildBatchesAsync(Date, true, false, default);

        result.Value.Preview.Should().ContainSingle();
        result.Value.Preview[0].Items.Should().ContainSingle();
        await _batches.DidNotReceiveWithAnyArgs()
            .AddRangeAsync(default!, default);
        await _batches.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(default);
    }

    [Fact]
    public async Task BuildBatches_Force_IsPassedOnlyToCoverageReaderAsync()
    {
        ArrangeEnabled();
        _orders.ReadEligibleAsync(Date, true, default)
            .Returns(Array.Empty<ConfirmedOrderDto>());

        await CreateSut().BuildBatchesAsync(Date, false, true, default);

        await _orders.Received(1).ReadEligibleAsync(Date, true, default);
    }

    [Fact]
    public async Task BuildBatches_MissingMarketProduct_ReturnsValidationFailureAsync()
    {
        var productId = Guid.NewGuid();
        ArrangeSingleOrder(productId);
        _markets.ReadMarketsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
            .Returns(new Dictionary<Guid, Guid>());

        var result = await CreateSut().BuildBatchesAsync(Date, false, false, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MARKET_PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task BuildBatches_MissingHub_RejectsBeforeWritingAsync()
    {
        var productId = Guid.NewGuid();
        ArrangeSingleOrder(productId);
        _markets.ReadMarketsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
            .Returns(new Dictionary<Guid, Guid> { [productId] = Guid.NewGuid() });
        _hubs.ReadActiveHubsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), default)
            .Returns(new Dictionary<Guid, Guid>());

        var result = await CreateSut().BuildBatchesAsync(Date, false, false, default);

        result.Error.Code.Should().Be("HUB_NOT_CONFIGURED_FOR_MARKET");
        await _batches.DidNotReceiveWithAnyArgs()
            .AddRangeAsync(default!, default);
    }

    private static readonly DateOnly Date = new(2026, 7, 15);

    private BatchConfirmedOrdersService CreateSut() =>
        new(_orders, _markets, _hubs, _settings, _batches);

    private void ArrangeEnabled() =>
        _settings.ReadAsync(default)
            .Returns(new ProcurementOperationalSettingsDto(true, new TimeOnly(22, 0)));

    private void ArrangeSingleOrder(Guid productId)
    {
        ArrangeEnabled();
        _orders.ReadEligibleAsync(Date, false, default).Returns(
        [
            new ConfirmedOrderDto(
                Guid.NewGuid(),
                Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                [new ConfirmedOrderItemDto(productId, "Tomato", 3)])
        ]);
    }
}
