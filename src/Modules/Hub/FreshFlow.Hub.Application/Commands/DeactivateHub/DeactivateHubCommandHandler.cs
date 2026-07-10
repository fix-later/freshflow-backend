using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Commands.DeactivateHub;

internal sealed class DeactivateHubCommandHandler(IHubRepository hubs)
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

        hub.Deactivate();
        await hubs.SaveChangesAsync(ct);

        return Result<HubDto>.Success(hub.ToDto());
    }
}
