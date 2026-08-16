using FreshFlow.Auth.Application.Commands.CreateAvatarUploadSignature;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Auth.Application.Commands.CreateLicenseUploadSignature;

internal sealed class CreateLicenseUploadSignatureCommandHandler(ICloudinarySignatureService signer)
    : IRequestHandler<CreateLicenseUploadSignatureCommand, Result<UploadSignatureResponse>>
{
    private const string LicenseFolder = "freshflow/licenses";

    public Task<Result<UploadSignatureResponse>> Handle(
        CreateLicenseUploadSignatureCommand request, CancellationToken ct)
    {
        var signed = signer.Sign(new CloudinarySignatureRequest(LicenseFolder));

        return Task.FromResult(Result<UploadSignatureResponse>.Success(
            new UploadSignatureResponse(
                signed.Signature,
                signed.Timestamp,
                signed.ApiKey,
                signed.CloudName,
                signed.Folder)));
    }
}
