namespace FreshFlow.Auth.Application.Commands.CreateAvatarUploadSignature;

public sealed record UploadSignatureResponse(
    string Signature,
    long Timestamp,
    string ApiKey,
    string CloudName,
    string Folder);
