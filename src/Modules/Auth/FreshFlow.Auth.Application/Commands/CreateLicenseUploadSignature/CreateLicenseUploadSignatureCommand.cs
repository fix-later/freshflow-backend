using FreshFlow.Auth.Application.Commands.CreateAvatarUploadSignature;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.CreateLicenseUploadSignature;

public sealed record CreateLicenseUploadSignatureCommand : ICommand<UploadSignatureResponse>;
