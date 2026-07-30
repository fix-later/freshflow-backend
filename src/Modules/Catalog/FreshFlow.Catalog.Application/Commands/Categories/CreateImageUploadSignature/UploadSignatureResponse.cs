namespace FreshFlow.Catalog.Application.Commands.Categories.CreateImageUploadSignature;

public sealed record UploadSignatureResponse(
    string Signature,
    long Timestamp,
    string ApiKey,
    string CloudName,
    string Folder);
