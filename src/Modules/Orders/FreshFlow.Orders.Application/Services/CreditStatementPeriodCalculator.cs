namespace FreshFlow.Orders.Application.Services;

/// <summary>
/// Computes UTC month-boundary timestamps for credit statements using Asia/Ho_Chi_Minh as
/// the period-boundary timezone (DEC-CRE-03). Boundaries are computed in VN local time then
/// converted to UTC for storage/comparison — <c>PeriodStart</c> is inclusive, <c>PeriodEnd</c>
/// is exclusive (the first instant of the following month).
/// </summary>
public static class CreditStatementPeriodCalculator
{
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public static (DateTime PeriodStart, DateTime PeriodEnd) ResolvePeriod(int year, int month)
    {
        var periodStartLocal = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var periodEndLocal = periodStartLocal.AddMonths(1);

        var periodStartUtc = TimeZoneInfo.ConvertTimeToUtc(periodStartLocal, VietnamTimeZone);
        var periodEndUtc = TimeZoneInfo.ConvertTimeToUtc(periodEndLocal, VietnamTimeZone);

        return (periodStartUtc, periodEndUtc);
    }

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
