using System.ComponentModel.DataAnnotations;

namespace FreshFlow.API.Assistant.Llm;

/// <summary>
/// Binds to config section "Assistant:ZenMux". <see cref="ApiKey"/> is never hardcoded — it is
/// supplied via the environment variable Assistant__ZenMux__ApiKey (precedent: Email.ResendApiKey).
/// Validated with DataAnnotations + ValidateOnStart so a missing key fails fast at boot, not on the
/// first chat request.
/// </summary>
public sealed class ZenMuxOptions
{
    [Required]
    public string BaseUrl { get; init; } = "https://zenmux.ai/api/v1";

    [Required]
    public string Model { get; init; } = "z-ai/glm-5.2-free";

    [Required]
    public string ApiKey { get; init; } = string.Empty;

    [Range(1, 600)]
    public int TimeoutSeconds { get; init; } = 60;

    [Range(1, 20)]
    public int MaxToolHops { get; init; } = 6;
}
