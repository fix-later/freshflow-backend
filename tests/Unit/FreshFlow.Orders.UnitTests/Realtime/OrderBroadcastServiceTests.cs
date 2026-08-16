using FreshFlow.Orders.Application.Dtos;
using FreshFlow.Orders.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Realtime;

[Trait("Category", "Unit")]
public sealed class OrderBroadcastServiceTests
{
    private readonly IHubContext<OrderHub> _hubContext = Substitute.For<IHubContext<OrderHub>>();
    private readonly IHubClients _clients = Substitute.For<IHubClients>();
    private readonly IClientProxy _restaurantGroup = Substitute.For<IClientProxy>();
    private readonly IClientProxy _adminGroup = Substitute.For<IClientProxy>();
    private readonly OrderBroadcastService _sut;

    private static readonly Guid RestaurantId = Guid.NewGuid();

    public OrderBroadcastServiceTests()
    {
        _hubContext.Clients.Returns(_clients);
        _clients.Group($"restaurant:{RestaurantId}").Returns(_restaurantGroup);
        _clients.Group("admin:orders").Returns(_adminGroup);

        _sut = new OrderBroadcastService(_hubContext);
    }

    private static OrderStatusChangedBroadcastDto BuildDto(Guid? restaurantId = null) =>
        new(
            OrderId: Guid.NewGuid(),
            RestaurantId: restaurantId ?? RestaurantId,
            PreviousStatus: "confirmed",
            NewStatus: "delivering",
            ChangedAt: DateTime.UtcNow,
            EstimatedDeliveryAt: null);

    [Fact]
    public async Task BroadcastStatusChangedAsync_TargetsRestaurantGroupAsync()
    {
        var dto = BuildDto();

        await _sut.BroadcastStatusChangedAsync(dto);

        _clients.Received(1).Group($"restaurant:{dto.RestaurantId}");
    }

    [Fact]
    public async Task BroadcastStatusChangedAsync_TargetsAdminOrdersGroupAsync()
    {
        var dto = BuildDto();

        await _sut.BroadcastStatusChangedAsync(dto);

        _clients.Received(1).Group("admin:orders");
    }

    [Fact]
    public async Task BroadcastStatusChangedAsync_SendsOrderStatusChangedMethodNameAsync()
    {
        var dto = BuildDto();

        await _sut.BroadcastStatusChangedAsync(dto);

        await _restaurantGroup.Received(1).SendCoreAsync(
            "OrderStatusChanged",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
        await _adminGroup.Received(1).SendCoreAsync(
            "OrderStatusChanged",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastStatusChangedAsync_SendsDtoAsFirstArgumentAsync()
    {
        var dto = BuildDto();

        await _sut.BroadcastStatusChangedAsync(dto);

        await _restaurantGroup.Received(1).SendCoreAsync(
            Arg.Any<string>(),
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], dto)),
            Arg.Any<CancellationToken>());
        await _adminGroup.Received(1).SendCoreAsync(
            Arg.Any<string>(),
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], dto)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastStatusChangedAsync_PassesCancellationTokenAsync()
    {
        var dto = BuildDto();
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        await _sut.BroadcastStatusChangedAsync(dto, ct);

        await _restaurantGroup.Received(1).SendCoreAsync(
            Arg.Any<string>(),
            Arg.Any<object?[]>(),
            ct);
        await _adminGroup.Received(1).SendCoreAsync(
            Arg.Any<string>(),
            Arg.Any<object?[]>(),
            ct);
    }

    [Fact]
    public async Task BroadcastStatusChangedAsync_DifferentRestaurants_TargetsOwnGroupsAsync()
    {
        var restaurantA = Guid.NewGuid();
        var restaurantB = Guid.NewGuid();
        var groupA = Substitute.For<IClientProxy>();
        var groupB = Substitute.For<IClientProxy>();

        _clients.Group($"restaurant:{restaurantA}").Returns(groupA);
        _clients.Group($"restaurant:{restaurantB}").Returns(groupB);

        var dtoA = BuildDto(restaurantA);
        var dtoB = BuildDto(restaurantB);

        await _sut.BroadcastStatusChangedAsync(dtoA);
        await _sut.BroadcastStatusChangedAsync(dtoB);

        await groupA.Received(1).SendCoreAsync(
            "OrderStatusChanged", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        await groupB.Received(1).SendCoreAsync(
            "OrderStatusChanged", Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
        await groupA.DidNotReceive().SendCoreAsync(
            Arg.Any<string>(),
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], dtoB)),
            Arg.Any<CancellationToken>());
        await groupB.DidNotReceive().SendCoreAsync(
            Arg.Any<string>(),
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], dtoA)),
            Arg.Any<CancellationToken>());
    }
}
