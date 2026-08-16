using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Auth.Application.Commands.CreateAvatarUploadSignature;

public sealed record CreateAvatarUploadSignatureCommand : ICommand<UploadSignatureResponse>;
