using System.ComponentModel.DataAnnotations;

namespace FreshFlow.API.Assistant;

/// <summary>
/// Binds to config section "Assistant". Holds orchestration-level settings shared across every LLM
/// provider: the ordered failover chain (<see cref="Providers"/>) and the tool-hop budget
/// (<see cref="MaxToolHops"/>). Per-provider settings (model, timeout, auth) live in their own
/// sub-sections ("Assistant:ZenMux", "Assistant:Gemini").
/// </summary>
public sealed class AssistantOptions
{
    /// <summary>
    /// Provider keys in priority order — the first is tried first, later ones are fallbacks used when
    /// a higher-priority provider fails recoverably (e.g. quota/rate-limit). Valid values are the
    /// constants on <see cref="AssistantProviders"/>. Only providers listed here are registered and
    /// have their config validated at boot, so running a single provider needs no config for the rest.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "Assistant:Providers must list at least one provider.")]
    public string[] Providers { get; init; } = [];

    /// <summary>Max LLM ↔ tool round-trips per chat turn before the orchestrator stops.</summary>
    [Range(1, 20)]
    public int MaxToolHops { get; init; } = 6;
}

/// <summary>Canonical provider keys used in config and failover resolution.</summary>
public static class AssistantProviders
{
    public const string ZenMux = "ZenMux";
    public const string Gemini = "Gemini";
}
