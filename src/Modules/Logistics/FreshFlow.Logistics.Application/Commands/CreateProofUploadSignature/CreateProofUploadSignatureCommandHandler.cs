using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Logistics.Application.Commands.CreateProofUploadSignature;

internal sealed class CreateProofUploadSignatureCommandHandler(
    IDeliveryRepository deliveries,
    IDeliveryRouteRepository routes,
    ICloudinarySignatureService signer)
    : IRequestHandler<CreateProofUploadSignatureCommand, Result<UploadSignatureResponse>>
{
    private const string ProofFolder = "freshflow/proof-of-delivery";

    public async Task<Result<UploadSignatureResponse>> Handle(
        CreateProofUploadSignatureCommand request,
        CancellationToken ct)
    {
        var delivery = await deliveries.FindByIdAsync(request.DeliveryId, ct);
        if (delivery is null)
            return Result<UploadSignatureResponse>.Failure(Error.NotFound("DELIVERY", request.DeliveryId));

        var route = await routes.FindByIdAsync(delivery.DeliveryRouteId, ct);
        if (route is null)
            return Result<UploadSignatureResponse>.Failure(Error.NotFound("DELIVERY_ROUTE", delivery.DeliveryRouteId));

        if (route.DriverUserId != request.DriverUserId)
        {
            return Result<UploadSignatureResponse>.Failure(
                Error.Unauthorized("FORBIDDEN", "This delivery is not assigned to the authenticated driver."));
        }

        var signed = signer.Sign(new CloudinarySignatureRequest(ProofFolder));

        return Result<UploadSignatureResponse>.Success(
            new UploadSignatureResponse(
                signed.Signature,
                signed.Timestamp,
                signed.ApiKey,
                signed.CloudName,
                signed.Folder));
    }
}
