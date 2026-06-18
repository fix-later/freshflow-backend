using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;

namespace FreshFlow.Orders.Application.Dtos;

public sealed record OrderIssueDto(
    Guid IssueId,
    Guid OrderId,
    Guid? OrderItemId,
    Guid ReportedBy,
    string IssueType,
    decimal AffectedQuantity,
    string Description,
    string Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt);

internal static class OrderIssueDtoMapper
{
    public static OrderIssueDto ToDto(OrderIssue issue) => new(
        issue.Id,
        issue.OrderId,
        issue.OrderItemId,
        issue.ReportedBy,
        ToApiIssueType(issue.IssueType),
        issue.AffectedQuantity,
        issue.Description,
        ToApiStatus(issue.Status),
        issue.CreatedAt,
        issue.ResolvedAt);

    private static string ToApiIssueType(OrderIssueType issueType) => issueType switch
    {
        OrderIssueType.Missing => "missing",
        OrderIssueType.Wrong => "wrong",
        OrderIssueType.Damaged => "damaged",
        _ => issueType.ToString().ToLowerInvariant()
    };

    private static string ToApiStatus(OrderIssueStatus status) => status switch
    {
        OrderIssueStatus.Open => "open",
        OrderIssueStatus.Resolved => "resolved",
        _ => status.ToString().ToLowerInvariant()
    };
}
