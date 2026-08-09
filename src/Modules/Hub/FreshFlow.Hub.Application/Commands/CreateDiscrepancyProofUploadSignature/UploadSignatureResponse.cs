namespace FreshFlow.Hub.Application.Commands.CreateDiscrepancyProofUploadSignature;

public sealed record UploadSignatureResponse(
    string Signature,
    long Timestamp,
    string ApiKey,
    string CloudName,
    string Folder);
