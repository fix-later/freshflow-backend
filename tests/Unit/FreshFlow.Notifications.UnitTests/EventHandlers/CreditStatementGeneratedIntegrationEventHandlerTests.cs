using FluentAssertions;
using FreshFlow.Contracts;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Application.EventHandlers;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;
using FreshFlow.SharedKernel.Application;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FreshFlow.Notifications.UnitTests.EventHandlers;

[Trait("Category", "Unit")]
public sealed class CreditStatementGeneratedIntegrationEventHandlerTests
{
    private readonly INotificationRecipientResolver _recipients =
        Substitute.For<INotificationRecipientResolver>();

    private readonly INotificationWriter _writer = Substitute.For<INotificationWriter>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly CreditStatementGeneratedIntegrationEventHandler _sut;

    public CreditStatementGeneratedIntegrationEventHandlerTests()
    {
        var logger = Substitute.For<ILogger<CreditStatementGeneratedIntegrationEventHandler>>();
        _sut = new CreditStatementGeneratedIntegrationEventHandler(_recipients, _writer, _emailSender, logger);
        _emailSender.SendAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(),
                Arg.Any<EmailAttachment?>())
            .Returns(Result.Success());
        _writer.WriteAsync(
                Arg.Any<Guid>(), Arg.Any<NotificationType>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(call => new Notification(
                (Guid)call[0]!, (NotificationType)call[1]!, (string)call[2]!, (string)call[3]!, null));
    }

    private static CreditStatementGeneratedIntegrationEvent Event(
        Guid restaurantId, byte[]? statementPdf = null, string? statementPdfFileName = null) =>
        new(
            restaurantId,
            Guid.NewGuid(),
            new DateTime(2026, 5, 31, 17, 0, 0, DateTimeKind.Utc),  // June VN period start
            new DateTime(2026, 6, 30, 17, 0, 0, DateTimeKind.Utc),  // July VN period end
            1_500_000m,
            new DateTime(2026, 7, 15, 17, 0, 0, DateTimeKind.Utc),  // due = period end + 15d
            DateTime.UtcNow,
            statementPdf,
            statementPdfFileName);

    [Fact]
    public async Task Handle_WithResolvedRecipient_WritesInAppAndSendsEmailAsync()
    {
        var restaurantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _recipients.ResolveRecipientByRestaurantIdAsync(restaurantId, default)
            .Returns(new NotificationRecipient(userId, "owner@example.com"));

        await _sut.Handle(Event(restaurantId), default);

        await _writer.Received(1).WriteAsync(
            userId,
            NotificationType.credit_statement,
            "Sao kê công nợ hàng tháng",
            Arg.Is<string>(body =>
                body.Contains("06/2026", StringComparison.Ordinal) &&      // period month (VN)
                body.Contains("16/07/2026", StringComparison.Ordinal)),    // due = period end + 15d, VN
            Arg.Any<IReadOnlyDictionary<string, object?>>(),
            default);
        await _emailSender.Received(1).SendAsync(
            "owner@example.com", "Sao kê công nợ hàng tháng", Arg.Any<string>(), default);
    }

    [Fact]
    public async Task Handle_RecipientMissing_SkipsBothChannelsAsync()
    {
        var restaurantId = Guid.NewGuid();
        _recipients.ResolveRecipientByRestaurantIdAsync(restaurantId, default)
            .Returns((NotificationRecipient?)null);

        await _sut.Handle(Event(restaurantId), default);

        await _writer.DidNotReceive().WriteAsync(
            Arg.Any<Guid>(), Arg.Any<NotificationType>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>());
        await _emailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InAppWriteThrows_StillSendsEmailAsync()
    {
        var restaurantId = Guid.NewGuid();
        _recipients.ResolveRecipientByRestaurantIdAsync(restaurantId, default)
            .Returns(new NotificationRecipient(Guid.NewGuid(), "owner@example.com"));
        _writer.WriteAsync(
                Arg.Any<Guid>(), Arg.Any<NotificationType>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Notification>(new InvalidOperationException("db down")));

        var act = () => _sut.Handle(Event(restaurantId), default);

        await act.Should().NotThrowAsync();
        await _emailSender.Received(1).SendAsync(
            "owner@example.com", Arg.Any<string>(), Arg.Any<string>(), default);
    }

    [Fact]
    public async Task Handle_EventCarriesStatementPdf_SendsEmailWithAttachmentAsync()
    {
        var restaurantId = Guid.NewGuid();
        var pdfBytes = new byte[] { 1, 2, 3 };
        _recipients.ResolveRecipientByRestaurantIdAsync(restaurantId, default)
            .Returns(new NotificationRecipient(Guid.NewGuid(), "owner@example.com"));

        await _sut.Handle(Event(restaurantId, pdfBytes, "statement-2026-06.pdf"), default);

        await _emailSender.Received(1).SendAsync(
            "owner@example.com", Arg.Any<string>(), Arg.Any<string>(), default,
            Arg.Is<EmailAttachment>(a =>
                a.FileName == "statement-2026-06.pdf" && a.Content == pdfBytes && a.ContentType == "application/pdf"));
    }

    [Fact]
    public async Task Handle_EventHasNoStatementPdf_SendsEmailWithoutAttachmentAsync()
    {
        var restaurantId = Guid.NewGuid();
        _recipients.ResolveRecipientByRestaurantIdAsync(restaurantId, default)
            .Returns(new NotificationRecipient(Guid.NewGuid(), "owner@example.com"));

        await _sut.Handle(Event(restaurantId), default);

        await _emailSender.Received(1).SendAsync(
            "owner@example.com", Arg.Any<string>(), Arg.Any<string>(), default, attachment: null);
    }
}
