namespace FreshFlow.Notifications.Infrastructure.Push;

internal sealed class ExpoPushOptions
{
    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = "https://exp.host/--/api/v2/push/send";
    public string? AccessToken { get; init; }
    public int TimeoutSeconds { get; init; } = 10;
}
