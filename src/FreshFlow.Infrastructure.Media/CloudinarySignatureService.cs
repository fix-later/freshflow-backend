using CloudinaryDotNet;
using FreshFlow.SharedKernel.Application;
using Microsoft.Extensions.Options;

namespace FreshFlow.Infrastructure.Media;

internal sealed class CloudinarySignatureService(
    IOptions<CloudinaryOptions> options,
    TimeProvider timeProvider) : ICloudinarySignatureService
{
    public CloudinarySignatureResult Sign(CloudinarySignatureRequest request)
    {
        var settings = options.Value;
        var timestamp = timeProvider.GetUtcNow().ToUnixTimeSeconds();

        var parameters = new SortedDictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["timestamp"] = timestamp,
            ["folder"] = request.Folder
        };

        if (request.Extra is not null)
        {
            foreach (var (key, value) in request.Extra)
                parameters.TryAdd(key, value);
        }

        var cloudinary = new Cloudinary(new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret));
        var signature = cloudinary.Api.SignParameters(parameters);

        return new CloudinarySignatureResult(
            signature,
            timestamp,
            settings.ApiKey,
            settings.CloudName,
            request.Folder);
    }
}
