using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Products.CreateImageUploadSignature;

internal sealed class CreateProductImageUploadSignatureCommandHandler(ICloudinarySignatureService signer)
    : IRequestHandler<CreateProductImageUploadSignatureCommand, Result<UploadSignatureResponse>>
{
    private const string ProductFolder = "freshflow/products";

    public Task<Result<UploadSignatureResponse>> Handle(
        CreateProductImageUploadSignatureCommand request, CancellationToken ct)
    {
        var signed = signer.Sign(new CloudinarySignatureRequest(ProductFolder));

        return Task.FromResult(Result<UploadSignatureResponse>.Success(
            new UploadSignatureResponse(
                signed.Signature,
                signed.Timestamp,
                signed.ApiKey,
                signed.CloudName,
                signed.Folder)));
    }
}
