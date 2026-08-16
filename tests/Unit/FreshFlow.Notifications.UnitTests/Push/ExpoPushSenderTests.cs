using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;
using FreshFlow.Notifications.Infrastructure.Push;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FreshFlow.Notifications.UnitTests.Push;

[Trait("Category", "Unit")]
public sealed class ExpoPushSenderTests
{
    [Fact]
    public async Task SendAsync_NoActiveMobileDevices_DoesNotCallHttpAsync()
    {
        var repository = Substitute.For<INotificationDeviceRepository>();
        repository.GetActiveMobileAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var handler = new RecordingHandler(_ => OkTickets(1));
        var sut = CreateSender(repository, handler);

        var result = await sut.SendAsync(CreateNotification(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeTrue();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_AcceptedTicket_SendsExpectedPayloadAndAuthorizationAsync()
    {
        var userId = Guid.NewGuid();
        var repository = Substitute.For<INotificationDeviceRepository>();
        repository.GetActiveMobileAsync(userId, Arg.Any<CancellationToken>())
            .Returns([CreateDevice(userId, "ExponentPushToken[token-1]", 1)]);
        var handler = new RecordingHandler(_ => OkTickets(1));
        var sut = CreateSender(repository, handler, "expo-secret");
        var notification = CreateNotification(userId);

        var result = await sut.SendAsync(notification, default);

        result.IsSuccess.Should().BeTrue();
        handler.Requests.Should().ContainSingle();
        handler.Authorization.Should().Equal("Bearer expo-secret");
        using var json = JsonDocument.Parse(handler.Requests[0]);
        var message = json.RootElement[0];
        message.GetProperty("to").GetString().Should().Be("ExponentPushToken[token-1]");
        message.GetProperty("title").GetString().Should().Be(notification.Title);
        message.GetProperty("body").GetString().Should().Be(notification.Body);
        message.GetProperty("sound").GetString().Should().Be("default");
        message.GetProperty("data").GetProperty("notificationId").GetGuid().Should().Be(notification.Id);
        message.GetProperty("data").GetProperty("type").GetString().Should().Be("delivery_update");
        message.GetProperty("data").GetProperty("payload").GetString().Should().Be(notification.Payload);
    }

    [Fact]
    public async Task SendAsync_OneHundredOneDevices_SendsBatchesOfAtMostOneHundredAsync()
    {
        var userId = Guid.NewGuid();
        var repository = Substitute.For<INotificationDeviceRepository>();
        repository.GetActiveMobileAsync(userId, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, 101)
                .Select(index => CreateDevice(userId, $"token-{index}", index))
                .ToArray());
        var handler = new RecordingHandler(request =>
        {
            var count = JsonDocument.Parse(request).RootElement.GetArrayLength();
            return OkTickets(count);
        });
        var sut = CreateSender(repository, handler);

        var result = await sut.SendAsync(CreateNotification(userId), default);

        result.IsSuccess.Should().BeTrue();
        handler.Requests.Select(body => JsonDocument.Parse(body).RootElement.GetArrayLength())
            .Should().Equal(100, 1);
    }

    [Fact]
    public async Task SendAsync_MixedAcceptedAndDeviceNotRegistered_RevokesOnlyInvalidTokenAsync()
    {
        var userId = Guid.NewGuid();
        var repository = Substitute.For<INotificationDeviceRepository>();
        repository.GetActiveMobileAsync(userId, Arg.Any<CancellationToken>())
            .Returns(
            [
                CreateDevice(userId, "invalid-token", 1),
                CreateDevice(userId, "valid-token", 2),
            ]);
        var handler = new RecordingHandler(_ => JsonResponse(
            """
            {"data":[
              {"status":"error","message":"not registered","details":{"error":"DeviceNotRegistered"}},
              {"status":"ok","id":"ticket-id"}
            ]}
            """));
        var sut = CreateSender(repository, handler);

        var result = await sut.SendAsync(CreateNotification(userId), default);

        result.IsSuccess.Should().BeTrue();
        await repository.Received(1).RevokeTokenAsync("invalid-token", Arg.Any<CancellationToken>());
        await repository.DidNotReceive().RevokeTokenAsync("valid-token", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task SendAsync_RetryableHttpFailure_ReturnsFailureAsync(HttpStatusCode status)
    {
        var userId = Guid.NewGuid();
        var repository = Substitute.For<INotificationDeviceRepository>();
        repository.GetActiveMobileAsync(userId, Arg.Any<CancellationToken>())
            .Returns([CreateDevice(userId, "token", 1)]);
        var handler = new RecordingHandler(_ => new HttpResponseMessage(status));
        var sut = CreateSender(repository, handler);

        var result = await sut.SendAsync(CreateNotification(userId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EXPO_PUSH_FAILED");
    }

    [Fact]
    public async Task SendAsync_AllTicketsRejected_ReturnsFailureAsync()
    {
        var userId = Guid.NewGuid();
        var repository = Substitute.For<INotificationDeviceRepository>();
        repository.GetActiveMobileAsync(userId, Arg.Any<CancellationToken>())
            .Returns([CreateDevice(userId, "token", 1)]);
        var handler = new RecordingHandler(_ => JsonResponse(
            """{"data":[{"status":"error","message":"bad payload","details":{"error":"MessageTooBig"}}]}"""));
        var sut = CreateSender(repository, handler);

        var result = await sut.SendAsync(CreateNotification(userId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("bad payload");
        await repository.DidNotReceive()
            .RevokeTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_NetworkFailure_ReturnsFailureAsync()
    {
        var userId = Guid.NewGuid();
        var repository = Substitute.For<INotificationDeviceRepository>();
        repository.GetActiveMobileAsync(userId, Arg.Any<CancellationToken>())
            .Returns([CreateDevice(userId, "token", 1)]);
        var handler = new RecordingHandler(_ => throw new HttpRequestException("offline"));
        var sut = CreateSender(repository, handler);

        var result = await sut.SendAsync(CreateNotification(userId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EXPO_PUSH_FAILED");
    }

    [Fact]
    public async Task SendAsync_MalformedResponse_ReturnsFailureAsync()
    {
        var userId = Guid.NewGuid();
        var repository = Substitute.For<INotificationDeviceRepository>();
        repository.GetActiveMobileAsync(userId, Arg.Any<CancellationToken>())
            .Returns([CreateDevice(userId, "token", 1)]);
        var handler = new RecordingHandler(_ => JsonResponse("not-json"));
        var sut = CreateSender(repository, handler);

        var result = await sut.SendAsync(CreateNotification(userId), default);

        result.IsFailure.Should().BeTrue();
    }

    private static ExpoPushSender CreateSender(
        INotificationDeviceRepository repository,
        RecordingHandler handler,
        string? accessToken = null) =>
        new(
            new HttpClient(handler),
            repository,
            new ExpoPushOptions
            {
                BaseUrl = "https://exp.host/--/api/v2/push/send",
                AccessToken = accessToken,
            },
            NullLogger<ExpoPushSender>.Instance);

    private static Notification CreateNotification(Guid userId) =>
        new(
            userId,
            NotificationType.delivery_update,
            "Delivery started",
            "Your order is on the way.",
            """{"order_id":"123"}""");

    private static NotificationDevice CreateDevice(Guid userId, string token, int index) =>
        new(userId, token, NotificationDevicePlatform.ios, $"device-{index}");

    private static HttpResponseMessage OkTickets(int count) =>
        JsonResponse(JsonSerializer.Serialize(new
        {
            data = Enumerable.Range(0, count).Select(index => new
            {
                status = "ok",
                id = $"ticket-{index}",
            }),
        }));

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class RecordingHandler(
        Func<string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public List<string> Authorization { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Requests.Add(body);
            if (request.Headers.Authorization is not null)
                Authorization.Add(request.Headers.Authorization.ToString());
            return respond(body);
        }
    }
}
