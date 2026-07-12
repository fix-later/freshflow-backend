namespace FreshFlow.Logistics.Application.Commands.ReportDeliveryIssue;

public sealed record DeliveryIssueDto(
    Guid Id,
    Guid DeliveryId,
    string IssueType,
    string Description,
    string Status,
    DateTime CreatedAt);
