namespace FreshFlow.Catalog.Application.Commands.Markets.CreateImageUploadSignature;

public sealed record UploadSignatureResponse(
    string Signature,
    long Timestamp,
    string ApiKey,
    string CloudName,
    string Folder);
