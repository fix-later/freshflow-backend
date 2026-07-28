namespace FreshFlow.Logistics.Infrastructure.CrossModule;

/// <summary>
/// Converts an <c>Asia/Ho_Chi_Minh</c> business date into its UTC <c>[start, end)</c> window so
/// UTC <c>timestamptz</c> columns (e.g. <c>orders."ScheduledFor"</c>) can be filtered by VN calendar
/// day. Mirrors <c>Analytics.Application.Common.VietnamTime</c>, which is <c>internal</c> to another
/// module and cannot be referenced across the module boundary.
/// </summary>
internal static class VietnamTime
{
    public static TimeZoneInfo Zone { get; } = ResolveTimeZone();

    /// <summary>UTC bounds for a single VN calendar day: [00:00 VN, next-day 00:00 VN).</summary>
    public static (DateTime StartUtcInclusive, DateTime EndUtcExclusive) GetUtcDayBounds(DateOnly day)
    {
        var localStart = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEnd = day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

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
