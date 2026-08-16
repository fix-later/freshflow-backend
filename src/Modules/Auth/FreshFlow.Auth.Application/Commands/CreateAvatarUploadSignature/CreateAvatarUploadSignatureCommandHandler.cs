using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.CreateAvatarUploadSignature;

internal sealed class CreateAvatarUploadSignatureCommandHandler(ICloudinarySignatureService signer)
    : IRequestHandler<CreateAvatarUploadSignatureCommand, Result<UploadSignatureResponse>>
{
    private const string AvatarFolder = "freshflow/avatars";

    public Task<Result<UploadSignatureResponse>> Handle(
        CreateAvatarUploadSignatureCommand request, CancellationToken ct)
    {
        var signed = signer.Sign(new CloudinarySignatureRequest(AvatarFolder));

        return Task.FromResult(Result<UploadSignatureResponse>.Success(
            new UploadSignatureResponse(
                signed.Signature,
                signed.Timestamp,
                signed.ApiKey,
                signed.CloudName,
                signed.Folder)));
    }
}
