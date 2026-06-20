using FreshFlow.Orders.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Orders.Application.Commands.ReportOrderIssue;

/// <summary>
/// SCRUM-225 — UC-ORD-19: restaurant reports a missing/wrong/damaged delivered order issue.
/// </summary>
public sealed record ReportOrderIssueCommand(
    Guid UserId,
    Guid OrderId,
    Guid? OrderItemId,
    string IssueType,
    decimal AffectedQuantity,
    string Description) : ICommand<OrderIssueDto>;
