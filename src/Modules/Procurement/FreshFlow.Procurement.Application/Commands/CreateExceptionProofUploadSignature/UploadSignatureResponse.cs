namespace FreshFlow.Procurement.Application.Commands.CreateExceptionProofUploadSignature;

public sealed record UploadSignatureResponse(
    string Signature,
    long Timestamp,
    string ApiKey,
    string CloudName,
    string Folder);
