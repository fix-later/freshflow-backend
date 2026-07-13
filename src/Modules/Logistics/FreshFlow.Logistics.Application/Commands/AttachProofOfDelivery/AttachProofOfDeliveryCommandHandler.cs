using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.AttachProofOfDelivery;

internal sealed class AttachProofOfDeliveryCommandHandler(
    IDeliveryRepository deliveries,
    IDeliveryRouteRepository routes)
    : IRequestHandler<AttachProofOfDeliveryCommand, Result<AttachProofOfDeliveryResponse>>
{
    public async Task<Result<AttachProofOfDeliveryResponse>> Handle(
        AttachProofOfDeliveryCommand request,
        CancellationToken ct)
    {
        var delivery = await deliveries.FindByIdAsync(request.DeliveryId, ct);
        if (delivery is null)
            return Result<AttachProofOfDeliveryResponse>.Failure(Error.NotFound("DELIVERY", request.DeliveryId));

        var route = await routes.FindByIdAsync(delivery.DeliveryRouteId, ct);
        if (route is null)
        {
            return Result<AttachProofOfDeliveryResponse>.Failure(
                Error.NotFound("DELIVERY_ROUTE", delivery.DeliveryRouteId));
        }

        if (route.DriverUserId != request.DriverUserId)
        {
            return Result<AttachProofOfDeliveryResponse>.Failure(
                Error.Unauthorized("FORBIDDEN", "This delivery is not assigned to the authenticated driver."));
        }

        delivery.AttachProof(request.ProofUrl);
        await deliveries.SaveChangesAsync(ct);

        return Result<AttachProofOfDeliveryResponse>.Success(
            new AttachProofOfDeliveryResponse(delivery.Id, delivery.ProofUrl!));
    }
}
