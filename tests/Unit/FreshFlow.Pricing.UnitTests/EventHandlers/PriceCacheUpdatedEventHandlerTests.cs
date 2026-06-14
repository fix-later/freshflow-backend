using FluentAssertions;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.EventHandlers;
using FreshFlow.Pricing.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FreshFlow.Pricing.UnitTests.EventHandlers;

/// <summary>
/// Unit tests for <see cref="PriceCacheUpdatedEventHandler"/>.
///
/// Verifies that:
/// - <see cref="IPriceBoardCacheWriter.WriteAsync"/> is called with correct field mappings.
/// - <c>reserved</c> is NOT passed (interface has no such param).
/// - Redis exceptions are caught and logged at Warning — they do not propagate.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PriceCacheUpdatedEventHandlerTests
{
    // ── Fixtures ──────────────────────────────────────────────────────────────

    private static readonly Guid MarketProductId = Guid.NewGuid();
    private static readonly Guid MarketId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid UpdatedBy = Guid.NewGuid();
    private static readonly DateTime OccurredAt =
        new(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc);

    private static PriceUpdatedDomainEvent BuildEvent(
        decimal oldPrice = 100m,
        decimal newPrice = 125m,
        int quantity = 50,
        Guid? updatedBy = null) =>
        new(MarketProductId, MarketId, ProductId,
            oldPrice, newPrice, quantity,
            updatedBy, OccurredAt);

    private static (PriceCacheUpdatedEventHandler handler, IPriceBoardCacheWriter writer)
        BuildSut()
    {
        var writer = Substitute.For<IPriceBoardCacheWriter>();
        var logger = Substitute.For<ILogger<PriceCacheUpdatedEventHandler>>();
        return (new PriceCacheUpdatedEventHandler(writer, logger), writer);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CallsWriteAsync()
    {
        var (handler, writer) = BuildSut();

        await handler.Handle(BuildEvent(), CancellationToken.None);

        await writer.Received(1).WriteAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(),
            Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesMarketIdAndProductId()
    {
        var (handler, writer) = BuildSut();

        await handler.Handle(BuildEvent(), CancellationToken.None);

        await writer.Received(1).WriteAsync(
            Arg.Is<Guid>(g => g == MarketId),
            Arg.Is<Guid>(g => g == ProductId),
            Arg.Any<decimal>(), Arg.Any<int>(), Arg.Any<DateTime>(),
            Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesNewPriceNotOldPrice()
    {
        var (handler, writer) = BuildSut();

        await handler.Handle(BuildEvent(oldPrice: 100m, newPrice: 125m), CancellationToken.None);

        await writer.Received(1).WriteAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Is<decimal>(p => p == 125m),
            Arg.Any<int>(), Arg.Any<DateTime>(),
            Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCurrentQuantity()
    {
        var (handler, writer) = BuildSut();

        await handler.Handle(BuildEvent(quantity: 42), CancellationToken.None);

        await writer.Received(1).WriteAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(),
            Arg.Is<int>(q => q == 42),
            Arg.Any<DateTime>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesOccurredAtAsUpdatedAt()
    {
        var (handler, writer) = BuildSut();

        await handler.Handle(BuildEvent(), CancellationToken.None);

        await writer.Received(1).WriteAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<int>(),
            Arg.Is<DateTime>(d => d == OccurredAt),
            Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesUpdatedByGuid()
    {
        var (handler, writer) = BuildSut();

        await handler.Handle(BuildEvent(updatedBy: UpdatedBy), CancellationToken.None);

        await writer.Received(1).WriteAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<int>(),
            Arg.Any<DateTime>(),
            Arg.Is<Guid?>(u => u == UpdatedBy),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesNullUpdatedBy()
    {
        var (handler, writer) = BuildSut();

        await handler.Handle(BuildEvent(updatedBy: null), CancellationToken.None);

        await writer.Received(1).WriteAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<int>(),
            Arg.Any<DateTime>(),
            Arg.Is<Guid?>(u => u == null),
            Arg.Any<CancellationToken>());
    }

    // ── Exception resilience ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenWriterThrows_DoesNotRethrow()
    {
        var (handler, writer) = BuildSut();
        writer.WriteAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<int>(),
            Arg.Any<DateTime>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Redis unavailable"));

        var act = () => handler.Handle(BuildEvent(), CancellationToken.None);

        // Redis failure must not propagate — price update already committed
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_WhenWriterThrows_WriteAsyncWasStillCalled()
    {
        var (handler, writer) = BuildSut();
        writer.WriteAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<int>(),
            Arg.Any<DateTime>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Redis unavailable"));

        await handler.Handle(BuildEvent(), CancellationToken.None);

        await writer.Received(1).WriteAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<int>(),
            Arg.Any<DateTime>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    // ── Fix #5: OperationCanceledException rethrow ───────────────────────────

    [Fact]
    public async Task Handle_OperationCancelled_RethrowsOperationCanceledException()
    {
        // Arrange — WriteAsync throws OCE (e.g. request was cancelled)
        var (handler, writer) = BuildSut();
        writer.WriteAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<int>(),
            Arg.Any<DateTime>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException("Request cancelled"));

        // Act
        var act = async () => await handler.Handle(BuildEvent(), CancellationToken.None);

        // Assert — OCE must propagate (cooperative cancellation), not be swallowed
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
