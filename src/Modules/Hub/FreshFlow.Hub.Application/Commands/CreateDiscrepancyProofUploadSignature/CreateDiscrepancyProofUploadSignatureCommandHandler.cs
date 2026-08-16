using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Hub.Application.Commands.CreateDiscrepancyProofUploadSignature;

internal sealed class CreateDiscrepancyProofUploadSignatureCommandHandler(
    IHubInboundRepository inbounds,
    ICloudinarySignatureService signer)
    : IRequestHandler<CreateDiscrepancyProofUploadSignatureCommand, Result<UploadSignatureResponse>>
{
    private const string ProofFolder = "freshflow/hub-discrepancies";

    public async Task<Result<UploadSignatureResponse>> Handle(
        CreateDiscrepancyProofUploadSignatureCommand request,
        CancellationToken ct)
    {
        if (!await inbounds.ExistsForHubAsync(request.HubId, request.InboundEventId, ct))
        {
            return Result<UploadSignatureResponse>.Failure(
                Error.NotFound("HUB_INBOUND_EVENT", request.InboundEventId));
        }

        var signed = signer.Sign(new CloudinarySignatureRequest(ProofFolder));
        return Result<UploadSignatureResponse>.Success(new UploadSignatureResponse(
            signed.Signature,
            signed.Timestamp,
            signed.ApiKey,
            signed.CloudName,
            signed.Folder));
    }
}
