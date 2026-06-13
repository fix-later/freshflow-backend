using FluentAssertions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.Pricing.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace FreshFlow.Pricing.UnitTests.Realtime;

[Trait("Category", "Unit")]
public sealed class PricingBroadcastServiceTests
{
    private readonly IHubContext<PricingHub> _hubContext =
        Substitute.For<IHubContext<PricingHub>>();

    private readonly IHubClients _clients =
        Substitute.For<IHubClients>();

    private readonly IClientProxy _group =
        Substitute.For<IClientProxy>();

    private readonly PricingBroadcastService _sut;

    private static readonly Guid MarketId = Guid.NewGuid();

    public PricingBroadcastServiceTests()
    {
        _hubContext.Clients.Returns(_clients);
        _clients.Group($"market:{MarketId}").Returns(_group);

        _sut = new PricingBroadcastService(_hubContext);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static PriceUpdateBroadcastDto BuildDto(Guid? marketId = null) =>
        new(
            MarketProductId: Guid.NewGuid(),
            MarketId: marketId ?? MarketId,
            ProductId: Guid.NewGuid(),
            OldPrice: 100_000m,
            NewPrice: 120_000m,
            CurrentQuantity: 300,
            UpdatedBy: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow);

    // ── Group targeting ───────────────────────────────────────────────────────

    [Fact]
    public async Task BroadcastPriceUpdateAsync_TargetsCorrectMarketGroup()
    {
        // Arrange
        var dto = BuildDto();

        // Act
        await _sut.BroadcastPriceUpdateAsync(dto);

        // Assert — must target group "market:{marketId}" not a different group
        _clients.Received(1).Group($"market:{dto.MarketId}");
    }

    [Fact]
    public async Task BroadcastPriceUpdateAsync_DoesNotTargetWrongMarketGroup()
    {
        // Arrange
        var dto = BuildDto();
        var wrongMarketId = Guid.NewGuid();

        // Act
        await _sut.BroadcastPriceUpdateAsync(dto);

        // Assert — must NOT target a different market group
        _clients.DidNotReceive().Group($"market:{wrongMarketId}");
    }

    // ── Method name ───────────────────────────────────────────────────────────

    [Fact]
    public async Task BroadcastPriceUpdateAsync_SendsPriceUpdatedMethodName()
    {
        // Arrange
        var dto = BuildDto();

        // Act
        await _sut.BroadcastPriceUpdateAsync(dto);

        // Assert — method name must be exactly "PriceUpdated" (client-side handler key)
        await _group.Received(1).SendCoreAsync(
            "PriceUpdated",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }

    // ── Payload ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task BroadcastPriceUpdateAsync_SendsDtoAsFirstArgument()
    {
        // Arrange
        var dto = BuildDto();

        // Act
        await _sut.BroadcastPriceUpdateAsync(dto);

        // Assert — dto is passed as the single argument in the args array
        await _group.Received(1).SendCoreAsync(
            Arg.Any<string>(),
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], dto)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastPriceUpdateAsync_PassesCancellationToken()
    {
        // Arrange
        var dto = BuildDto();
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        // Act
        await _sut.BroadcastPriceUpdateAsync(dto, ct);

        // Assert
        await _group.Received(1).SendCoreAsync(
            Arg.Any<string>(),
            Arg.Any<object?[]>(),
            ct);
    }

    // ── Invariant: different markets stay isolated ────────────────────────────

    [Fact]
    public async Task BroadcastPriceUpdateAsync_TwoDifferentMarkets_EachTargetsOwnGroup()
    {
        // Arrange
        var marketA = Guid.NewGuid();
        var marketB = Guid.NewGuid();
        var groupA = Substitute.For<IClientProxy>();
        var groupB = Substitute.For<IClientProxy>();

        _clients.Group($"market:{marketA}").Returns(groupA);
        _clients.Group($"market:{marketB}").Returns(groupB);

        var dtoA = BuildDto(marketId: marketA);
        var dtoB = BuildDto(marketId: marketB);

        // Act
        await _sut.BroadcastPriceUpdateAsync(dtoA);
        await _sut.BroadcastPriceUpdateAsync(dtoB);

        // Assert — each market group received exactly one broadcast, not the other's
        await groupA.Received(1).SendCoreAsync(
            "PriceUpdated", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        await groupB.Received(1).SendCoreAsync(
            "PriceUpdated", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        // groupA must not have received marketB's broadcast (and vice versa via group isolation above)
        await groupA.DidNotReceive().SendCoreAsync(
            Arg.Any<string>(),
            Arg.Is<object?[]>(a => a.Length == 1 && ReferenceEquals(a[0], dtoB)),
            Arg.Any<CancellationToken>());
    }
}
