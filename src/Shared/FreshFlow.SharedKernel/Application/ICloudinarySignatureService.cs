namespace FreshFlow.SharedKernel.Application;

public interface ICloudinarySignatureService
{
    public CloudinarySignatureResult Sign(CloudinarySignatureRequest request);
}

public sealed record CloudinarySignatureRequest(
    string Folder,
    IReadOnlyDictionary<string, object>? Extra = null);

public sealed record CloudinarySignatureResult(
    string Signature,
    long Timestamp,
    string ApiKey,
    string CloudName,
    string Folder);
