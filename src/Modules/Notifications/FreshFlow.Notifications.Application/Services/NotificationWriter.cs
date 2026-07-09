using System.Text.Json;
using System.Text.Json.Serialization;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;

namespace FreshFlow.Notifications.Application.Services;

public sealed class NotificationWriter(INotificationRepository notifications) : INotificationWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<Notification> WriteAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        IReadOnlyDictionary<string, object?>? payload,
        CancellationToken ct)
    {
        var notification = new Notification(
            userId,
            type,
            title,
            body,
            SerializePayload(payload));

        return await notifications.AddAsync(notification, ct);
    }

    private static string? SerializePayload(IReadOnlyDictionary<string, object?>? payload)
    {
        if (payload is null)
            return null;

        var normalizedPayload = payload
            .Where(item => item.Value is not null)
            .ToDictionary(item => item.Key, item => item.Value);

        return normalizedPayload.Count == 0
            ? null
            : JsonSerializer.Serialize(normalizedPayload, JsonOptions);
    }
}
