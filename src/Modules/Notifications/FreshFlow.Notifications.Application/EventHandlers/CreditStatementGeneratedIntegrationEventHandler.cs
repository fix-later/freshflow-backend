using System.Globalization;
using FreshFlow.Contracts;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Notifications.Application.EventHandlers;

/// <summary>
/// SCRUM-269 — on a newly generated monthly credit statement, notifies the restaurant owner
/// both in-app (persisted notification) and by email. The two channels are independent: one
/// failing is logged and never stops the other, and nothing here throws back into the publisher.
/// </summary>
internal sealed class CreditStatementGeneratedIntegrationEventHandler(
    INotificationRecipientResolver recipients,
    INotificationWriter writer,
    IEmailSender emailSender,
    ILogger<CreditStatementGeneratedIntegrationEventHandler> logger)
    : INotificationHandler<CreditStatementGeneratedIntegrationEvent>
{
    private const string Title = "Sao kê công nợ hàng tháng";

    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public async Task Handle(CreditStatementGeneratedIntegrationEvent notification, CancellationToken ct)
    {
        var recipient = await recipients.ResolveRecipientByRestaurantIdAsync(notification.RestaurantId, ct);
        if (recipient is null)
        {
            logger.LogWarning(
                "Skipping statement notification because RestaurantId={RestaurantId} has no owner user.",
                notification.RestaurantId);
            return;
        }

        var body = BuildBody(notification);

        await WriteInAppAsync(recipient, notification, body, ct);
        await SendEmailAsync(recipient, body, notification, ct);
    }

    private async Task WriteInAppAsync(
        NotificationRecipient recipient,
        CreditStatementGeneratedIntegrationEvent notification,
        string body,
        CancellationToken ct)
    {
        try
        {
            await writer.WriteAsync(
                recipient.UserId,
                NotificationType.credit_statement,
                Title,
                body,
                new Dictionary<string, object?>
                {
                    ["statementId"] = notification.StatementId,
                    ["periodStart"] = notification.PeriodStart,
                    ["periodEnd"] = notification.PeriodEnd,
                    ["closingBalance"] = notification.ClosingBalance,
                    ["dueDate"] = notification.DueDate,
                },
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex, "Failed to persist in-app statement notification for RestaurantId={RestaurantId}.",
                notification.RestaurantId);
        }
    }

    private async Task SendEmailAsync(
        NotificationRecipient recipient,
        string body,
        CreditStatementGeneratedIntegrationEvent notification,
        CancellationToken ct)
    {
        var attachment = notification.StatementPdf is not null
            ? new EmailAttachment(notification.StatementPdfFileName!, notification.StatementPdf, "application/pdf")
            : null;

        try
        {
            var result = await emailSender.SendAsync(recipient.Email, Title, body, ct, attachment);
            if (result.IsFailure)
                logger.LogWarning(
                    "Statement email to {Email} failed: {Code} {Message}",
                    recipient.Email, result.Error.Code, result.Error.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to send statement email to {Email}.", recipient.Email);
        }
    }

    private static string BuildBody(CreditStatementGeneratedIntegrationEvent n)
    {
        // Period boundaries and due date are stored UTC — convert to Asia/Ho_Chi_Minh before
        // formatting so the month label and due date read as the intended business dates.
        var periodLocal = TimeZoneInfo.ConvertTimeFromUtc(n.PeriodStart, VietnamTimeZone);
        var dueLocal = TimeZoneInfo.ConvertTimeFromUtc(n.DueDate, VietnamTimeZone);
        var closing = n.ClosingBalance.ToString("N0", CultureInfo.InvariantCulture);

        return
            $"Sao kê kỳ {periodLocal:MM/yyyy} đã được lập. " +
            $"Dư nợ cuối kỳ: {closing} đ. " +
            $"Vui lòng thanh toán trước ngày {dueLocal:dd/MM/yyyy}.";
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}
