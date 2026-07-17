namespace FreshFlow.Analytics.Application.Common;

internal static class VietnamTime
{
    public static TimeZoneInfo Zone { get; } = ResolveTimeZone();

    public static (DateTime StartUtcInclusive, DateTime EndUtcExclusive) GetUtcBounds(
        DateOnly from,
        DateOnly to)
    {
        var localStart = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEnd = to == DateOnly.MaxValue
            ? DateTime.MaxValue
            : to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        return (
            TimeZoneInfo.ConvertTimeToUtc(localStart, Zone),
            TimeZoneInfo.ConvertTimeToUtc(localEnd, Zone));
    }

    private static TimeZoneInfo ResolveTimeZone()
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
