using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FreshFlow.Notifications.Application.Abstractions;
using FreshFlow.Notifications.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Notifications.Infrastructure.Push;

internal sealed class ExpoPushSender(
    HttpClient httpClient,
    INotificationDeviceRepository devices,
    ExpoPushOptions options,
    ILogger<ExpoPushSender> logger) : IPushSender
{
    private const int MaxBatchSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result> SendAsync(Notification notification, CancellationToken ct)
    {
        var recipients = await devices.GetActiveMobileAsync(notification.UserId, ct);
        if (recipients.Count == 0)
            return Result.Success();

        var accepted = false;
        string? failure = null;

        foreach (var batch in recipients.Chunk(MaxBatchSize))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, options.BaseUrl)
            {
                Content = JsonContent.Create(
                    batch.Select(device => new ExpoMessage(
                        device.Token,
                        notification.Title,
                        notification.Body,
                        "default",
                        new ExpoData(
                            notification.Id,
                            notification.Type.ToString(),
                            notification.Payload))),
                    options: JsonOptions),
            };

            if (!string.IsNullOrWhiteSpace(options.AccessToken))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", options.AccessToken.Trim());
            }

            try
            {
                using var response = await httpClient.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    failure = $"Expo push request failed with HTTP {(int)response.StatusCode}.";
                    break;
                }

                var result = await response.Content.ReadFromJsonAsync<ExpoResponse>(JsonOptions, ct);
                if (result?.Data is null || result.Data.Count != batch.Length)
                {
                    failure = result?.Errors?.FirstOrDefault()?.Message
                        ?? "Expo push response did not contain one ticket per device.";
                    break;
                }

                for (var index = 0; index < result.Data.Count; index++)
                {
                    var ticket = result.Data[index];
                    if (ticket.Status == "ok")
                    {
                        accepted = true;
                        continue;
                    }

                    failure ??= ticket.Message ?? "Expo rejected the push notification.";
                    if (ticket.Details?.Error == "DeviceNotRegistered")
                        await devices.RevokeTokenAsync(batch[index].Token, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
            {
                logger.LogWarning(
                    ex,
                    "Expo push request failed for notification {NotificationId}.",
                    notification.Id);
                failure = "Expo push service is unavailable.";
                break;
            }
        }

        return accepted
            ? Result.Success()
            : Result.Failure(Error.Validation("EXPO_PUSH_FAILED", failure ?? "Expo rejected all push tickets."));
    }

    private sealed record ExpoMessage(
        string To,
        string Title,
        string Body,
        string Sound,
        ExpoData Data);

    private sealed record ExpoData(
        Guid NotificationId,
        string Type,
        string? Payload);

    private sealed record ExpoResponse(
        IReadOnlyList<ExpoTicket>? Data,
        IReadOnlyList<ExpoServiceError>? Errors);

    private sealed record ExpoTicket(
        string Status,
        string? Message,
        ExpoTicketDetails? Details);

    private sealed record ExpoTicketDetails(string? Error);

    private sealed record ExpoServiceError(string? Code, string? Message);
}
