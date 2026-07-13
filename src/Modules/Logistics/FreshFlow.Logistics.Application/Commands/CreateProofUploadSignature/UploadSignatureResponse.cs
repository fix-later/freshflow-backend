namespace FreshFlow.Logistics.Application.Commands.CreateProofUploadSignature;

public sealed record UploadSignatureResponse(
    string Signature,
    long Timestamp,
    string ApiKey,
    string CloudName,
    string Folder);
