namespace FreshFlow.Notifications.Infrastructure.CrossModule;

internal sealed class NotificationOrderRecipientRow
{
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
}
