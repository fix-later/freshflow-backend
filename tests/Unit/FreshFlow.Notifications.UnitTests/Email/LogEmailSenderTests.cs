using FluentAssertions;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreshFlow.Notifications.UnitTests.Email;

[Trait("Category", "Unit")]
public sealed class LogEmailSenderTests
{
    private readonly LogEmailSender _sut = new(NullLogger<LogEmailSender>.Instance);

    [Fact]
    public async Task SendAsync_NoAttachment_ReturnsSuccessAsync()
    {
        var result = await _sut.SendAsync("owner@example.com", "Subject", "Body", default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_WithAttachment_ReturnsSuccessAsync()
    {
        var attachment = new EmailAttachment("statement-2026-06.pdf", [1, 2, 3], "application/pdf");

        var result = await _sut.SendAsync("owner@example.com", "Subject", "Body", default, attachment);

        result.IsSuccess.Should().BeTrue();
    }
}
