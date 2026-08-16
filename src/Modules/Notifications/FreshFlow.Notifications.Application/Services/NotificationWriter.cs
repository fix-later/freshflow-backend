using System.Text.Json;
using System.Text.Json.Serialization;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Application.Mappers;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.Notifications.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Notifications.Application.Services;

public sealed class NotificationWriter(
    INotificationRepository notifications,
    IPushSender pushSender,
    INotificationBroadcastService broadcast,
    ILogger<NotificationWriter> logger) : INotificationWriter
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

        var persisted = await notifications.AddAsync(notification, ct);

        try
        {
            await broadcast.BroadcastCreatedAsync(persisted.UserId, persisted.ToDto(), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to broadcast notification {NotificationId} to user {UserId}.",
                persisted.Id,
                persisted.UserId);
        }

        try
        {
            var result = await pushSender.SendAsync(persisted, ct);
            if (result.IsSuccess)
                persisted.MarkSent();
            else
                persisted.MarkFailed(
                    string.IsNullOrWhiteSpace(result.Error.Message) ? "Unknown error" : result.Error.Message);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            persisted.MarkFailed(ex.Message);
        }

        await notifications.UpdateAsync(persisted, ct);
        return persisted;
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
