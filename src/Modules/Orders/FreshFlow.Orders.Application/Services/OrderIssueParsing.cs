using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.Application.Services;

internal static class OrderIssueParsing
{
    public static bool TryParseIssueType(string? raw, out OrderIssueType issueType)
    {
        issueType = default;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var normalized = raw.Trim();
        issueType = normalized.ToLowerInvariant() switch
        {
            "missing" => OrderIssueType.Missing,
            "wrong" => OrderIssueType.Wrong,
            "damaged" => OrderIssueType.Damaged,
            _ => default
        };

        return normalized.Equals("missing", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("wrong", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("damaged", StringComparison.OrdinalIgnoreCase);
    }
}
