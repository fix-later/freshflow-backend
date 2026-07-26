using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Commands.DeactivateHub;

internal sealed class DeactivateHubCommandHandler(
    IHubRepository hubs,
    IHubProcurementPlanReader procurement)
    : IRequestHandler<DeactivateHubCommand, Result<HubDto>>
{
    public async Task<Result<HubDto>> Handle(DeactivateHubCommand request, CancellationToken ct)
    {
        var hub = await hubs.FindByIdAsync(request.HubId, ct);
        if (hub is null)
            return Result<HubDto>.Failure(Error.NotFound("HUB", request.HubId));

        if (await hubs.HasPendingInboundAsync(request.HubId, ct))
        {
            return Result<HubDto>.Failure(
                Error.Validation(
                    "HUB_HAS_PENDING_DELIVERIES",
                    "Hub cannot be deactivated while it has pending inbound deliveries."));
        }

        if (await procurement.HasOpenBatchesAsync(request.HubId, ct))
        {
            return Result<HubDto>.Failure(
                Error.Conflict(
                    "HUB_HAS_ACTIVE_PROCUREMENT",
                    // ponytail: pause batching for Hub routing changes; add a shared DB row lock
                    // if admin deactivation and batching must run concurrently.
                    "Hub cannot be deactivated while it has active procurement batches."));
        }

        hub.Deactivate();
        try
        {
            await hubs.SaveChangesAsync(ct);
        }
        catch (HubConcurrencyException)
        {
            return Result<HubDto>.Failure(
                Error.Conflict(
                    "OPTIMISTIC_CONCURRENCY_CONFLICT",
                    "Hub was updated by another request. Please refresh and retry."));
        }

        return Result<HubDto>.Success(hub.ToDto());
    }
}
