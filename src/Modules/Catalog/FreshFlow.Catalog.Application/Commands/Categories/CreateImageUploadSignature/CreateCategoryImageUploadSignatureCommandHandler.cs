using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Categories.CreateImageUploadSignature;

internal sealed class CreateCategoryImageUploadSignatureCommandHandler(ICloudinarySignatureService signer)
    : IRequestHandler<CreateCategoryImageUploadSignatureCommand, Result<UploadSignatureResponse>>
{
    private const string CategoryFolder = "freshflow/categories";

    public Task<Result<UploadSignatureResponse>> Handle(
        CreateCategoryImageUploadSignatureCommand request, CancellationToken ct)
    {
        var signed = signer.Sign(new CloudinarySignatureRequest(CategoryFolder));

        return Task.FromResult(Result<UploadSignatureResponse>.Success(
            new UploadSignatureResponse(
                signed.Signature,
                signed.Timestamp,
                signed.ApiKey,
                signed.CloudName,
                signed.Folder)));
    }
}
