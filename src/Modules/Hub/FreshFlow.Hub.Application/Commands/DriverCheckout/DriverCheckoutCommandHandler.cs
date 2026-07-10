using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Application.Dtos;
using FreshFlow.Hub.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Commands.DriverCheckout;

internal sealed class DriverCheckoutCommandHandler(IHubHandoverRepository handovers)
    : IRequestHandler<DriverCheckoutCommand, Result<HubHandoverDto>>
{
    public async Task<Result<HubHandoverDto>> Handle(DriverCheckoutCommand request, CancellationToken ct)
    {
        var handover = await handovers.FindByIdAsync(request.HubId, request.HandoverId, ct);
        if (handover is null)
            return Result<HubHandoverDto>.Failure(Error.NotFound("HUB_HANDOVER", request.HandoverId));

        if (request.DriverUserId != handover.DriverUserId)
            return Result<HubHandoverDto>.Failure(Error.Unauthorized("FORBIDDEN", "This handover is not assigned to the authenticated driver."));

        try
        {
            handover.ConfirmCheckout(request.DriverUserId);
        }
        catch (InvalidOperationException)
        {
            return Result<HubHandoverDto>.Failure(
                Error.Conflict("HUB_HANDOVER_ALREADY_CHECKED_OUT", "Hub handover has already been checked out."));
        }

        await handovers.SaveChangesAsync(ct);
        return Result<HubHandoverDto>.Success(handover.ToDto());
    }
}
