using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.Application.Queries;

internal static class OrderQueryParsing
{
    public const string DefaultSort = "createdAt:desc";

    public static bool TryParseStatus(string? value, out OrderStatus status)
    {
        status = default;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim().Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant();
        status = normalized switch
        {
            "draft" or "pending" => OrderStatus.Draft,
            "confirmed" => OrderStatus.Confirmed,
            "batched" or "processing" => OrderStatus.Batched,
            "picked_up" or "pickedup" or "ready_for_pickup" => OrderStatus.PickedUp,
            "at_hub" or "athub" => OrderStatus.AtHub,
            "delivering" or "in_transit" => OrderStatus.Delivering,
            "delivered" => OrderStatus.Delivered,
            "cancelled" or "canceled" => OrderStatus.Cancelled,
            _ => default
        };

        return normalized is "draft" or "pending"
            or "confirmed"
            or "batched" or "processing"
            or "picked_up" or "pickedup" or "ready_for_pickup"
            or "at_hub" or "athub"
            or "delivering" or "in_transit"
            or "delivered"
            or "cancelled" or "canceled";
    }

    public static bool TryParseSort(string? value, out bool ascending)
    {
        ascending = false;

        if (string.IsNullOrWhiteSpace(value))
            return true;

        var normalized = value.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        if (normalized == "createdat:desc")
            return true;

        if (normalized == "createdat:asc")
        {
            ascending = true;
            return true;
        }

        return false;
    }

    public static DateTime? NormalizeCreatedTo(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        var dateTime = value.Value;
        return dateTime.TimeOfDay == TimeSpan.Zero
            ? dateTime.Date.AddDays(1).AddTicks(-1)
            : dateTime;
    }
}
