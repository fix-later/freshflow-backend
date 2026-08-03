using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.Application.Queries;

internal static class OrderClaimQueryParsing
{
    public static bool TryParseStatus(string? value, out OrderClaimStatus? status)
    {
        status = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (!Enum.TryParse<OrderClaimStatus>(value, ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
            return false;

        status = parsed;
        return true;
    }
}
