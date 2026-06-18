using System.Security.Claims;
using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Orders.UnitTests.Realtime;

[Trait("Category", "Unit")]
public sealed class OrderHubTests
{
    private const string AdminRole = "admin";
    private const string OperationsManagerRole = "operations_manager";
    private const string RestaurantRole = "restaurant";
    private const string MarketAgentRole = "market_agent";
    private const string ConnectionId = "test-connection-id";

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();

    private readonly IRestaurantReader _restaurantReader = Substitute.For<IRestaurantReader>();
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly HubCallerContext _hubContext = Substitute.For<HubCallerContext>();

    private OrderHub BuildHub(string role, string? sub = null)
    {
        var claims = new List<Claim> { new("role", role) };
        if (sub is not null)
            claims.Add(new Claim("sub", sub));

        var user = new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            authenticationType: "test",
            nameType: "sub",
            roleType: "role"));

        _hubContext.User.Returns(user);
        _hubContext.ConnectionId.Returns(ConnectionId);
        _hubContext.ConnectionAborted.Returns(CancellationToken.None);

        return new OrderHub(_restaurantReader)
        {
            Context = _hubContext,
            Groups = _groups,
        };
    }

    [Fact]
    public async Task OnConnectedAsync_RestaurantUser_JoinsOwnRestaurantGroupAsync()
    {
        var hub = BuildHub(RestaurantRole, UserId.ToString());
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new RestaurantSnapshotDto(RestaurantId, IsApproved: true));

        await hub.OnConnectedAsync();

        await _groups.Received(1).AddToGroupAsync(
            ConnectionId,
            $"restaurant:{RestaurantId}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnConnectedAsync_AdminUser_JoinsAdminOrdersGroupWithoutRestaurantLookupAsync()
    {
        var hub = BuildHub(AdminRole, UserId.ToString());

        await hub.OnConnectedAsync();

        await _groups.Received(1).AddToGroupAsync(
            ConnectionId,
            "admin:orders",
            Arg.Any<CancellationToken>());
        await _restaurantReader.DidNotReceive().FindByUserIdAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnConnectedAsync_OperationsManagerUser_JoinsAdminOrdersGroupAsync()
    {
        var hub = BuildHub(OperationsManagerRole, UserId.ToString());

        await hub.OnConnectedAsync();

        await _groups.Received(1).AddToGroupAsync(
            ConnectionId,
            "admin:orders",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnConnectedAsync_RestaurantUserWithoutProfile_ThrowsHubExceptionAsync()
    {
        var hub = BuildHub(RestaurantRole, UserId.ToString());
        _restaurantReader.FindByUserIdAsync(UserId, Arg.Any<CancellationToken>()).ReturnsNull();

        var act = async () => await hub.OnConnectedAsync();

        await act.Should().ThrowAsync<HubException>()
            .WithMessage("*Unable to determine caller restaurant*");
    }

    [Fact]
    public async Task OnConnectedAsync_RestaurantUserWithInvalidSub_ThrowsHubExceptionAsync()
    {
        var hub = BuildHub(RestaurantRole, "not-a-guid");

        var act = async () => await hub.OnConnectedAsync();

        await act.Should().ThrowAsync<HubException>()
            .WithMessage("*Unable to determine caller identity*");
        await _restaurantReader.DidNotReceive().FindByUserIdAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnConnectedAsync_UnrelatedRole_DoesNotJoinOrderGroupsAsync()
    {
        var hub = BuildHub(MarketAgentRole, UserId.ToString());

        await hub.OnConnectedAsync();

        await _groups.DidNotReceive().AddToGroupAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _restaurantReader.DidNotReceive().FindByUserIdAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
