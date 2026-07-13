using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Logistics.Application.Commands.ReportDeliveryIssue;

public sealed record ReportDeliveryIssueCommand(
    Guid DeliveryId,
    Guid DriverUserId,
    string IssueType,
    string Description) : ICommand<DeliveryIssueDto>;
