using FluentAssertions;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Application.Commands.RegisterDevice;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Notifications.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class RegisterDeviceCommandHandlerTests
{
    private readonly INotificationDeviceRepository _devices = Substitute.For<INotificationDeviceRepository>();
    private readonly RegisterDeviceCommandHandler _sut;

    public RegisterDeviceCommandHandlerTests()
    {
        _sut = new RegisterDeviceCommandHandler(_devices);
    }

    [Fact]
    public async Task Handle_NewDevice_ReturnsMappedDtoAsync()
    {
        var userId = Guid.NewGuid();
        var device = new NotificationDevice(
            userId,
            "push-token",
            NotificationDevicePlatform.ios,
            "device-1");
        _devices.RegisterAsync(
                userId,
                "push-token",
                NotificationDevicePlatform.ios,
                "device-1",
                default)
            .Returns(device);

        var result = await _sut.Handle(
            new RegisterDeviceCommand(userId, "push-token", "ios", "device-1"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(device.Id);
        result.Value.UserId.Should().Be(userId);
        result.Value.Token.Should().Be("push-token");
        result.Value.Platform.Should().Be("ios");
        result.Value.DeviceId.Should().Be("device-1");
        result.Value.RevokedAt.Should().BeNull();
        await _devices.Received(1).RegisterAsync(
            userId,
            "push-token",
            NotificationDevicePlatform.ios,
            "device-1",
            default);
    }

    [Fact]
    public async Task Handle_ReactivatedDevice_ReturnsUpdatedMappedDtoAsync()
    {
        var userId = Guid.NewGuid();
        var device = new NotificationDevice(
            userId,
            "push-token",
            NotificationDevicePlatform.web,
            "old-device");
        device.Revoke();
        device.Reactivate(NotificationDevicePlatform.android, "android-device");
        _devices.RegisterAsync(
                userId,
                "push-token",
                NotificationDevicePlatform.android,
                "android-device",
                default)
            .Returns(device);

        var result = await _sut.Handle(
            new RegisterDeviceCommand(userId, "push-token", "android", "android-device"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(device.Id);
        result.Value.Platform.Should().Be("android");
        result.Value.DeviceId.Should().Be("android-device");
        result.Value.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_InvalidPlatform_ReturnsValidationErrorAsync()
    {
        var result = await _sut.Handle(
            new RegisterDeviceCommand(Guid.NewGuid(), "push-token", "windows", null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
        await _devices.DidNotReceive().RegisterAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<NotificationDevicePlatform>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyUserId_ReturnsValidationErrorAsync()
    {
        var result = await _sut.Handle(
            new RegisterDeviceCommand(Guid.Empty, "push-token", "ios", null),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
        await _devices.DidNotReceive().RegisterAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<NotificationDevicePlatform>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyToken_ReturnsValidationErrorAsync()
    {
        var result = await _sut.Handle(
            new RegisterDeviceCommand(Guid.NewGuid(), "", "ios", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
        await _devices.DidNotReceive().RegisterAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<NotificationDevicePlatform>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
