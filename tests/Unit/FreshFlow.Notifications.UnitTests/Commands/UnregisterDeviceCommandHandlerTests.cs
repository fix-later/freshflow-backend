using FluentAssertions;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Application.Commands.UnregisterDevice;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;
using NSubstitute;

namespace FreshFlow.Notifications.UnitTests.Commands;

[Trait("Category", "Unit")]
public sealed class UnregisterDeviceCommandHandlerTests
{
    private readonly INotificationDeviceRepository _devices = Substitute.For<INotificationDeviceRepository>();
    private readonly UnregisterDeviceCommandHandler _sut;

    public UnregisterDeviceCommandHandlerTests()
    {
        _sut = new UnregisterDeviceCommandHandler(_devices);
    }

    [Fact]
    public async Task Handle_ExistingToken_ReturnsRevokedDeviceDtoAsync()
    {
        var userId = Guid.NewGuid();
        var device = new NotificationDevice(
            userId,
            "push-token",
            NotificationDevicePlatform.web,
            "device-1");
        device.Revoke();
        _devices.UnregisterAsync(userId, "push-token", default).Returns(device);

        var result = await _sut.Handle(
            new UnregisterDeviceCommand(userId, "push-token"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(device.Id);
        result.Value.UserId.Should().Be(userId);
        result.Value.Token.Should().Be("push-token");
        result.Value.Platform.Should().Be("web");
        result.Value.RevokedAt.Should().NotBeNull();
        await _devices.Received(1).UnregisterAsync(userId, "push-token", default);
    }

    [Fact]
    public async Task Handle_MissingToken_ReturnsNotFoundAsync()
    {
        var userId = Guid.NewGuid();
        _devices.UnregisterAsync(userId, "missing-token", default)
            .Returns((NotificationDevice?)null);

        var result = await _sut.Handle(
            new UnregisterDeviceCommand(userId, "missing-token"),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NOTIFICATIONDEVICE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_BlankToken_ReturnsValidationErrorAsync()
    {
        var result = await _sut.Handle(
            new UnregisterDeviceCommand(Guid.NewGuid(), " "),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
        await _devices.DidNotReceive().UnregisterAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyUserId_ReturnsValidationErrorAsync()
    {
        var result = await _sut.Handle(
            new UnregisterDeviceCommand(Guid.Empty, "push-token"),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("VALIDATION_ERROR");
        await _devices.DidNotReceive().UnregisterAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
