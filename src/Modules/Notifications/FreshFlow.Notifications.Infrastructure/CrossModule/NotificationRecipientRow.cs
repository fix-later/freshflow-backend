namespace FreshFlow.Notifications.Infrastructure.CrossModule;

/// <summary>
/// Infrastructure-only read projection onto restaurants owned by Auth.
/// </summary>
internal sealed class NotificationRecipientRow
{
    public Guid RestaurantId { get; set; }
    public Guid UserId { get; set; }
}
