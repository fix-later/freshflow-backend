namespace FreshFlow.Orders.Application.Services;

/// <summary>
/// Applies the daily 22:00 (Asia/Ho_Chi_Minh) order cutoff (FR-ORD-005, UC-ORD-07, SCRUM-196).
/// Restaurants pick a delivery date within a D..D+N window (D = local confirm date, N = the
/// admin-configured <c>operational_settings.delivery_window_days</c>, default 7, SCRUM-179).
/// The earliest valid delivery date is D+1 if confirmed before 22:00 local, or D+2 if at/after
/// the cutoff. A null or too-early requested schedule is normalized up to the earliest valid
/// date; the D+N upper bound is enforced separately by FluentValidation (DELIVERY_DATE_OUT_OF_WINDOW)
/// since it does not depend on the cutoff.
/// </summary>
public static class OrderCutoffScheduler
{
    /// <summary>Fallback used when no admin-configured cutoff (operational_settings, SCRUM-355) is supplied.</summary>
    public static readonly TimeSpan DefaultCutoffLocalTime = TimeSpan.FromHours(22);

    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public static DateTime? ResolveScheduledFor(
        DateTime confirmedAtUtc, DateTime? requestedScheduledFor, TimeSpan? cutoffLocalTime = null)
    {
        var earliestValidUtc = ResolveEarliestValidDelivery(confirmedAtUtc, cutoffLocalTime ?? DefaultCutoffLocalTime);

        if (requestedScheduledFor is null || requestedScheduledFor < earliestValidUtc)
            return earliestValidUtc;

        return requestedScheduledFor;
    }

    /// <summary>
    /// Validates the D..D+<paramref name="windowDays"/> delivery window upper bound and the
    /// no-past-date constraint (DELIVERY_DATE_OUT_OF_WINDOW). Does not depend on the cutoff
    /// time, so callers can use it directly in FluentValidation regardless of when confirm
    /// happens. <paramref name="windowDays"/> is the admin-configured
    /// <c>operational_settings.delivery_window_days</c> (default 7) — required, not defaulted
    /// here, so every caller must explicitly source it rather than silently falling back.
    /// </summary>
    public static bool IsWithinDeliveryWindow(DateTime nowUtc, DateTime scheduledFor, int windowDays)
    {
        if (scheduledFor < nowUtc)
            return false;

        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, VietnamTimeZone);
        var upperBoundLocalDate = nowLocal.Date.AddDays(windowDays);
        var upperBoundUtc = TimeZoneInfo.ConvertTimeToUtc(upperBoundLocalDate, VietnamTimeZone);

        return scheduledFor <= upperBoundUtc;
    }

    public static DateOnly GetServiceDate(DateTime scheduledForUtc)
    {
        var utc = scheduledForUtc.Kind switch
        {
            DateTimeKind.Utc => scheduledForUtc,
            DateTimeKind.Local => scheduledForUtc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(scheduledForUtc, DateTimeKind.Utc)
        };
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamTimeZone));
    }

    private static DateTime ResolveEarliestValidDelivery(DateTime confirmedAtUtc, TimeSpan cutoffLocalTime)
    {
        var confirmedAtLocal = TimeZoneInfo.ConvertTimeFromUtc(confirmedAtUtc, VietnamTimeZone);
        var isPastCutoff = confirmedAtLocal.TimeOfDay >= cutoffLocalTime;
        var earliestValidOffsetDays = isPastCutoff ? 2 : 1;
        var earliestValidLocalDate = confirmedAtLocal.Date.AddDays(earliestValidOffsetDays);

        return TimeZoneInfo.ConvertTimeToUtc(earliestValidLocalDate, VietnamTimeZone);
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
