using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using FreshFlow.Notifications.Infrastructure.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace FreshFlow.Notifications.UnitTests.Realtime;

[Trait("Category", "Unit")]
public sealed class NotificationHubTests
{
    private const string ConnectionId = "notification-connection";
    private readonly IGroupManager _groups = Substitute.For<IGroupManager>();
    private readonly HubCallerContext _context = Substitute.For<HubCallerContext>();

    [Fact]
    public async Task OnConnectedAsync_AuthenticatedUser_JoinsOwnUserGroupAsync()
    {
        var userId = Guid.NewGuid();
        var hub = BuildHub(userId.ToString());

        await hub.OnConnectedAsync();

        await _groups.Received(1).AddToGroupAsync(
            ConnectionId,
            $"user:{userId}",
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task OnConnectedAsync_InvalidSubject_RejectsConnectionAsync(string? subject)
    {
        var hub = BuildHub(subject);

        var action = () => hub.OnConnectedAsync();

        await action.Should().ThrowAsync<HubException>()
            .WithMessage("*Unable to determine caller identity*");
        await _groups.DidNotReceive().AddToGroupAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void NotificationHub_RequiresAuthenticationForEveryRole()
    {
        var attribute = typeof(NotificationHub).GetCustomAttribute<AuthorizeAttribute>();

        attribute.Should().NotBeNull();
        attribute!.Roles.Should().BeNull();
    }

    private NotificationHub BuildHub(string? subject)
    {
        var claims = subject is null ? [] : new[] { new Claim("sub", subject) };
        _context.User.Returns(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")));
        _context.ConnectionId.Returns(ConnectionId);
        _context.ConnectionAborted.Returns(CancellationToken.None);

        return new NotificationHub
        {
            Context = _context,
            Groups = _groups,
        };
    }
}
