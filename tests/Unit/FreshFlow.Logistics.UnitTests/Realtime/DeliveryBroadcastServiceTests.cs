using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Realtime;

[Trait("Category", "Unit")]
public sealed class DeliveryBroadcastServiceTests
{
    private static readonly Guid RestaurantId = Guid.NewGuid();

    private readonly IHubContext<DeliveryHub> _hubContext = Substitute.For<IHubContext<DeliveryHub>>();
    private readonly IHubClients _clients = Substitute.For<IHubClients>();
    private readonly IClientProxy _restaurantGroup = Substitute.For<IClientProxy>();
    private readonly IClientProxy _adminGroup = Substitute.For<IClientProxy>();
    private readonly DeliveryBroadcastService _sut;

    public DeliveryBroadcastServiceTests()
    {
        _hubContext.Clients.Returns(_clients);
        _clients.Group($"restaurant:{RestaurantId}").Returns(_restaurantGroup);
        _clients.Group("admin:delivery").Returns(_adminGroup);
        _sut = new DeliveryBroadcastService(_hubContext);
    }

    [Fact]
    public async Task BroadcastDeliveryStartedAsync_TargetsRestaurantAndAdminGroupsAsync()
    {
        var update = CreateUpdate("started");

        await _sut.BroadcastDeliveryStartedAsync(RestaurantId, update, default);

        _clients.Received(1).Group($"restaurant:{RestaurantId}");
        _clients.Received(1).Group("admin:delivery");
        await _restaurantGroup.Received(1).SendCoreAsync(
            "DeliveryStarted",
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], update)),
            Arg.Any<CancellationToken>());
        await _adminGroup.Received(1).SendCoreAsync(
            "DeliveryStarted",
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], update)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastDeliveryStopUpdatedAsync_TargetsRestaurantAndAdminGroupsAsync()
    {
        var update = CreateUpdate("delivered");

        await _sut.BroadcastDeliveryStopUpdatedAsync(RestaurantId, update, default);

        _clients.Received(1).Group($"restaurant:{RestaurantId}");
        _clients.Received(1).Group("admin:delivery");
        await _restaurantGroup.Received(1).SendCoreAsync(
            "DeliveryStopUpdated",
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], update)),
            Arg.Any<CancellationToken>());
        await _adminGroup.Received(1).SendCoreAsync(
            "DeliveryStopUpdated",
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], update)),
            Arg.Any<CancellationToken>());
    }

    private static DeliveryRealtimeUpdate CreateUpdate(string status) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            status,
            DateTime.UtcNow);
}
