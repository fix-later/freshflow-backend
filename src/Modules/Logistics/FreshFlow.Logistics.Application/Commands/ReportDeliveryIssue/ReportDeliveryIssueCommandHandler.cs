using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.ReportDeliveryIssue;

internal sealed class ReportDeliveryIssueCommandHandler(
    IDeliveryRepository deliveries,
    IDeliveryRouteRepository routes,
    IDeliveryIssueRepository issues)
    : IRequestHandler<ReportDeliveryIssueCommand, Result<DeliveryIssueDto>>
{
    public async Task<Result<DeliveryIssueDto>> Handle(
        ReportDeliveryIssueCommand request,
        CancellationToken ct)
    {
        var delivery = await deliveries.FindByIdAsync(request.DeliveryId, ct);
        if (delivery is null)
            return Result<DeliveryIssueDto>.Failure(Error.NotFound("DELIVERY", request.DeliveryId));

        var route = await routes.FindByIdAsync(delivery.DeliveryRouteId, ct);
        if (route is null)
        {
            return Result<DeliveryIssueDto>.Failure(
                Error.NotFound("DELIVERY_ROUTE", delivery.DeliveryRouteId));
        }

        if (route.DriverUserId != request.DriverUserId)
        {
            return Result<DeliveryIssueDto>.Failure(
                Error.Unauthorized("FORBIDDEN", "This delivery is not assigned to the authenticated driver."));
        }

        var issue = DeliveryIssue.Create(
            delivery.Id,
            request.DriverUserId,
            request.IssueType,
            request.Description);

        await issues.AddAsync(issue, ct);
        await issues.SaveChangesAsync(ct);

        return Result<DeliveryIssueDto>.Success(
            new DeliveryIssueDto(
                issue.Id,
                issue.DeliveryId,
                issue.IssueType,
                issue.Description,
                issue.Status,
                issue.CreatedAt));
    }
}
