using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.Application.Services;

internal static class ScheduledOrderParsing
{
    public static bool TryParseRecurrenceType(string? raw, out RecurrenceType recurrenceType)
    {
        recurrenceType = default;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        recurrenceType = raw.Trim().ToLowerInvariant() switch
        {
            "daily" => RecurrenceType.Daily,
            "weekly" => RecurrenceType.Weekly,
            _ => default
        };

        return raw.Trim().Equals("daily", StringComparison.OrdinalIgnoreCase)
            || raw.Trim().Equals("weekly", StringComparison.OrdinalIgnoreCase);
    }
}
