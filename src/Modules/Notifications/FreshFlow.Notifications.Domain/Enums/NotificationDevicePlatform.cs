namespace FreshFlow.Notifications.Domain.Enums;

public enum NotificationDevicePlatform
{
    ios,
    android,
    web
}

public static class NotificationDevicePlatformValues
{
    public static bool IsSupported(string? value) =>
        value is "ios" or "android" or "web";

    public static bool TryParse(string value, out NotificationDevicePlatform platform)
    {
        switch (value)
        {
            case "ios":
                platform = NotificationDevicePlatform.ios;
                return true;
            case "android":
                platform = NotificationDevicePlatform.android;
                return true;
            case "web":
                platform = NotificationDevicePlatform.web;
                return true;
            default:
                platform = default;
                return false;
        }
    }

    public static string ToApiValue(this NotificationDevicePlatform platform) => platform switch
    {
        NotificationDevicePlatform.ios => "ios",
        NotificationDevicePlatform.android => "android",
        NotificationDevicePlatform.web => "web",
        _ => throw new ArgumentOutOfRangeException(nameof(platform)),
    };
}
