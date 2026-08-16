using FreshFlow.Procurement.Application.Abstractions;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Procurement.Application.Commands.CreateExceptionProofUploadSignature;

internal sealed class CreateExceptionProofUploadSignatureCommandHandler(
    IProcurementBatchRepository batches,
    ICloudinarySignatureService signer)
    : IRequestHandler<CreateExceptionProofUploadSignatureCommand, Result<UploadSignatureResponse>>
{
    private const string ProofFolder = "freshflow/procurement-exceptions";

    public async Task<Result<UploadSignatureResponse>> Handle(
        CreateExceptionProofUploadSignatureCommand request,
        CancellationToken cancellationToken)
    {
        var batch = await batches.FindByIdAsync(request.BatchId, cancellationToken);
        if (batch is null ||
            !batch.Items.Any(item => item.AssignedAgentUserId == request.AgentUserId))
        {
            return Result<UploadSignatureResponse>.Failure(
                Error.NotFound("PROCUREMENT_BATCH", request.BatchId));
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
