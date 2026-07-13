using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Infrastructure.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace FreshFlow.Logistics.UnitTests.Realtime;

[Trait("Category", "Unit")]
public sealed class DeliveryHubTests
{
    private const string AdminRole = "admin";
    private const string OperationsManagerRole = "operations_manager";
    private const string RestaurantRole = "restaurant";
    private const string DriverRole = "driver";
    private const string ConnectionId = "delivery-test-connection";

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RestaurantId = Guid.NewGuid();

    private readonly IRestaurantOwnerReader _restaurantOwnerReader = Substitute.For<IRestaurantOwnerReader>();
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly HubCallerContext _hubContext = Substitute.For<HubCallerContext>();

    [Fact]
    public async Task OnConnectedAsync_RestaurantUser_JoinsOwnRestaurantGroupAsync()
    {
        var hub = BuildHub(RestaurantRole, UserId.ToString());
        _restaurantOwnerReader.FindRestaurantIdByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(RestaurantId);

        await hub.OnConnectedAsync();

        await _groups.Received(1).AddToGroupAsync(
            ConnectionId,
            $"restaurant:{RestaurantId}",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnConnectedAsync_AdminUser_JoinsAdminDeliveryGroupWithoutRestaurantLookupAsync()
    {
        var hub = BuildHub(AdminRole, UserId.ToString());

        await hub.OnConnectedAsync();

        await _groups.Received(1).AddToGroupAsync(
            ConnectionId,
            "admin:delivery",
            Arg.Any<CancellationToken>());
        await _restaurantOwnerReader.DidNotReceive().FindRestaurantIdByUserIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnConnectedAsync_OperationsManagerUser_JoinsAdminDeliveryGroupAsync()
    {
        var hub = BuildHub(OperationsManagerRole, UserId.ToString());

        await hub.OnConnectedAsync();

        await _groups.Received(1).AddToGroupAsync(
            ConnectionId,
            "admin:delivery",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnConnectedAsync_RestaurantUserWithoutProfile_ThrowsHubExceptionAsync()
    {
        var hub = BuildHub(RestaurantRole, UserId.ToString());
        _restaurantOwnerReader.FindRestaurantIdByUserIdAsync(UserId, Arg.Any<CancellationToken>()).ReturnsNull();

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
        await _restaurantOwnerReader.DidNotReceive().FindRestaurantIdByUserIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnConnectedAsync_UnrelatedRole_DoesNotJoinDeliveryGroupsAsync()
    {
        var hub = BuildHub(DriverRole, UserId.ToString());

        await hub.OnConnectedAsync();

        await _groups.DidNotReceive().AddToGroupAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DeliveryHub_RestrictsConnectionToRestaurantAndOpsRoles()
    {
        var attr = typeof(DeliveryHub).GetCustomAttribute<AuthorizeAttribute>();

        attr.Should().NotBeNull();
        attr!.Roles.Should().Be("admin,operations_manager,restaurant");
    }

    private DeliveryHub BuildHub(string role, string? sub)
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

        return new DeliveryHub(_restaurantOwnerReader)
        {
            Context = _hubContext,
            Groups = _groups,
        };
    }
}
