using FluentAssertions;
using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.Pricing.Application.EventHandlers;
using FreshFlow.Pricing.Domain.Events;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FreshFlow.Pricing.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class PriceUpdatedDomainEventHandlerTests
{
    private readonly IPricingBroadcastService _broadcastService =
        Substitute.For<IPricingBroadcastService>();

    private readonly ILogger<PriceUpdatedDomainEventHandler> _logger =
        Substitute.For<ILogger<PriceUpdatedDomainEventHandler>>();

    private readonly PriceUpdatedDomainEventHandler _sut;

    private static readonly Guid MarketProductId = Guid.NewGuid();
    private static readonly Guid MarketId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private static readonly Guid UpdatedBy = Guid.NewGuid();

    public PriceUpdatedDomainEventHandlerTests()
    {
        _sut = new PriceUpdatedDomainEventHandler(_broadcastService, _logger);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static PriceUpdatedDomainEvent BuildEvent(
        decimal oldPrice = 100_000m,
        decimal newPrice = 120_000m,
        int quantity = 300,
        Guid? updatedBy = null) =>
        new(
            MarketProductId,
            MarketId,
            ProductId,
            oldPrice,
            newPrice,
            quantity,
            updatedBy ?? UpdatedBy,
            DateTime.UtcNow);

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidEvent_CallsBroadcastServiceOnce()
    {
        // Arrange
        var evt = BuildEvent();

        // Act
        await _sut.Handle(evt, default);

        // Assert
        await _broadcastService.Received(1)
            .BroadcastPriceUpdateAsync(Arg.Any<PriceUpdateBroadcastDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidEvent_DtoHasCorrectMarketProductId()
    {
        // Arrange
        var evt = BuildEvent();

        // Act
        await _sut.Handle(evt, default);

        // Assert
        await _broadcastService.Received(1).BroadcastPriceUpdateAsync(
            Arg.Is<PriceUpdateBroadcastDto>(d => d.MarketProductId == MarketProductId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidEvent_DtoHasCorrectMarketId()
    {
        // Arrange
        var evt = BuildEvent();

        // Act
        await _sut.Handle(evt, default);

        // Assert
        await _broadcastService.Received(1).BroadcastPriceUpdateAsync(
            Arg.Is<PriceUpdateBroadcastDto>(d => d.MarketId == MarketId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidEvent_DtoHasCorrectPrices()
    {
        // Arrange
        var evt = BuildEvent(oldPrice: 100_000m, newPrice: 135_000m);

        // Act
        await _sut.Handle(evt, default);

        // Assert
        await _broadcastService.Received(1).BroadcastPriceUpdateAsync(
            Arg.Is<PriceUpdateBroadcastDto>(d =>
                d.OldPrice == 100_000m &&
                d.NewPrice == 135_000m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidEvent_DtoHasCorrectQuantity()
    {
        // Arrange
        var evt = BuildEvent(quantity: 450);

        // Act
        await _sut.Handle(evt, default);

        // Assert
        await _broadcastService.Received(1).BroadcastPriceUpdateAsync(
            Arg.Is<PriceUpdateBroadcastDto>(d => d.CurrentQuantity == 450),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidEvent_DtoHasCorrectProductId()
    {
        // Arrange
        var evt = BuildEvent();

        // Act
        await _sut.Handle(evt, default);

        // Assert
        await _broadcastService.Received(1).BroadcastPriceUpdateAsync(
            Arg.Is<PriceUpdateBroadcastDto>(d => d.ProductId == ProductId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidEvent_DtoPreservesUpdatedBy()
    {
        // Arrange
        var actor = Guid.NewGuid();
        var evt = BuildEvent(updatedBy: actor);

        // Act
        await _sut.Handle(evt, default);

        // Assert
        await _broadcastService.Received(1).BroadcastPriceUpdateAsync(
            Arg.Is<PriceUpdateBroadcastDto>(d => d.UpdatedBy == actor),
            Arg.Any<CancellationToken>());
    }

    // ── Fault tolerance ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_BroadcastThrows_DoesNotPropagateException()
    {
        // Arrange — simulate transient SignalR failure
        _broadcastService
            .BroadcastPriceUpdateAsync(Arg.Any<PriceUpdateBroadcastDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));

        var evt = BuildEvent();

        // Act — must NOT throw (broadcast failure must not fail the price update)
        var act = async () => await _sut.Handle(evt, default);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_BroadcastThrows_LogsError()
    {
        // Arrange
        var exception = new InvalidOperationException("Hub disconnected");
        _broadcastService
            .BroadcastPriceUpdateAsync(Arg.Any<PriceUpdateBroadcastDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(exception);

        var evt = BuildEvent();

        // Act
        await _sut.Handle(evt, default);

        // Assert — error must be logged (not silently swallowed)
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            exception,
            Arg.Any<Func<object, Exception?, string>>());
    }
}
