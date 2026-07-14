namespace FreshFlow.Procurement.Application.Services;

public static class ProcurementBatchCycle
{
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public static DateOnly ResolveDueBatchDate(
        DateTimeOffset nowUtc,
        TimeOnly cutoffLocalTime)
    {
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, VietnamTimeZone);
        var cutoffDate = TimeOnly.FromDateTime(localNow.DateTime) >= cutoffLocalTime
            ? DateOnly.FromDateTime(localNow.DateTime)
            : DateOnly.FromDateTime(localNow.DateTime).AddDays(-1);
        return cutoffDate.AddDays(1);
    }

    public static DateOnly GetLocalDate(DateTimeOffset nowUtc) =>
        DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(nowUtc, VietnamTimeZone).DateTime);

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
