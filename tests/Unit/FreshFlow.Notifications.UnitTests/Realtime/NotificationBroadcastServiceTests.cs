using FreshFlow.Notifications.Application.Dtos;
using FreshFlow.Notifications.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace FreshFlow.Notifications.UnitTests.Realtime;

[Trait("Category", "Unit")]
public sealed class NotificationBroadcastServiceTests
{
    [Fact]
    public async Task BroadcastCreatedAsync_SendsDtoToOnlyRecipientUserGroupAsync()
    {
        var userId = Guid.NewGuid();
        var hubContext = Substitute.For<IHubContext<NotificationHub>>();
        var clients = Substitute.For<IHubClients>();
        var group = Substitute.For<IClientProxy>();
        hubContext.Clients.Returns(clients);
        clients.Group($"user:{userId}").Returns(group);
        var sut = new NotificationBroadcastService(hubContext);
        var dto = new NotificationDto(
            Guid.NewGuid(),
            "system",
            "Title",
            "Body",
            null,
            false,
            null,
            DateTime.UtcNow);

        await sut.BroadcastCreatedAsync(userId, dto, default);

        clients.Received(1).Group($"user:{userId}");
        await group.Received(1).SendCoreAsync(
            "NotificationCreated",
            Arg.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], dto)),
            Arg.Any<CancellationToken>());
    }
}
