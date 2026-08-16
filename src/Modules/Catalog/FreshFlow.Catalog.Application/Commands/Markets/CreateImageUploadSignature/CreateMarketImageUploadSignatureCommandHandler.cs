using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Markets.CreateImageUploadSignature;

internal sealed class CreateMarketImageUploadSignatureCommandHandler(ICloudinarySignatureService signer)
    : IRequestHandler<CreateMarketImageUploadSignatureCommand, Result<UploadSignatureResponse>>
{
    private const string MarketFolder = "freshflow/markets";

    public Task<Result<UploadSignatureResponse>> Handle(
        CreateMarketImageUploadSignatureCommand request, CancellationToken ct)
    {
        var signed = signer.Sign(new CloudinarySignatureRequest(MarketFolder));

        return Task.FromResult(Result<UploadSignatureResponse>.Success(
            new UploadSignatureResponse(
                signed.Signature,
                signed.Timestamp,
                signed.ApiKey,
                signed.CloudName,
                signed.Folder)));
    }
}
