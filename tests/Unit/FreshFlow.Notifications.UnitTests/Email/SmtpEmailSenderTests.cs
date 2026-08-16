using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreshFlow.Notifications.UnitTests.Email;

/// <summary>
/// No SMTP server is available in this test environment, so these only exercise the
/// message-building path (attachment attached + disposed) up to the point <see cref="SmtpClient"/>
/// fails to connect — they assert the call fails gracefully (never throws) rather than that mail
/// was actually delivered. Points at an unused loopback port so the connection is refused
/// immediately instead of timing out.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SmtpEmailSenderTests
{
    private static int GetUnusedLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static SmtpEmailSender BuildSut() =>
        new(
            new SmtpEmailOptions { Host = "127.0.0.1", Port = GetUnusedLoopbackPort(), FromAddress = "noreply@freshflow.test" },
            NullLogger<SmtpEmailSender>.Instance);

    [Fact]
    public async Task SendAsync_NoAttachment_FailsGracefullyWithoutThrowingAsync()
    {
        var sut = BuildSut();

        var result = await sut.SendAsync("owner@example.com", "Subject", "Body", default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EMAIL_SEND_FAILED");
    }

    [Fact]
    public async Task SendAsync_WithAttachment_BuildsAndDisposesAttachmentWithoutThrowingAsync()
    {
        var sut = BuildSut();
        var attachment = new EmailAttachment("statement-2026-06.pdf", [1, 2, 3], "application/pdf");

        var result = await sut.SendAsync("owner@example.com", "Subject", "Body", default, attachment);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EMAIL_SEND_FAILED");
    }

    [Fact]
    public async Task SendAsync_BlankRecipient_ReturnsValidationErrorAsync()
    {
        var sut = BuildSut();

        var result = await sut.SendAsync(" ", "Subject", "Body", default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("EMAIL_RECIPIENT_MISSING");
    }
}
