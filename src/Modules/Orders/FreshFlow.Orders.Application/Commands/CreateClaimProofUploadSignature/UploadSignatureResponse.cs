namespace FreshFlow.Orders.Application.Commands.CreateClaimProofUploadSignature;

public sealed record UploadSignatureResponse(
    string Signature,
    long Timestamp,
    string ApiKey,
    string CloudName,
    string Folder);
