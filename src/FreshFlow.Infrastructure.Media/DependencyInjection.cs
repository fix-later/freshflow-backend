using FreshFlow.SharedKernel.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FreshFlow.Infrastructure.Media;

public static class DependencyInjection
{
    public static IServiceCollection AddMediaModule(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddOptions<CloudinaryOptions>()
            .Bind(config.GetSection("Cloudinary"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICloudinarySignatureService, CloudinarySignatureService>();

        return services;
    }
}
