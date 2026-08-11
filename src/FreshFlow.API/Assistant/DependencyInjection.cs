using FluentValidation;
using FreshFlow.API.Assistant.Abstractions;
using FreshFlow.API.Assistant.Conversation;
using FreshFlow.API.Assistant.Dtos;
using FreshFlow.API.Assistant.Llm;
using FreshFlow.API.Assistant.Safety;
using FreshFlow.API.Assistant.Tools;
using MediatR;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace FreshFlow.API.Assistant;

/// <summary>
/// Wires the assistant's LLM providers, tool registry (T2) and conversation store (T3). Multiple
/// providers can be configured via "Assistant:Providers" (priority order); they are composed behind
/// a <see cref="FailoverChatClient"/> so a higher-priority provider that runs out of quota falls
/// back to the next. Only providers actually listed are registered and config-validated, so running
/// a single provider needs no config for the others.
/// </summary>
public static class DependencyInjection
{
    private const string ZenMuxHttpClientName = "Assistant.ZenMux";
    private const string GeminiHttpClientName = "Assistant.Gemini";

    public static IServiceCollection AddAssistant(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<AssistantOptions>()
            .Bind(config.GetSection("Assistant"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Read the configured priority list up front so only the providers in use get registered
        // (and validated at boot). Unknown names fail fast here with a clear message.
        var providers = (config.GetSection("Assistant:Providers").Get<string[]>() ?? [])
            .Select(NormalizeProvider)
            .ToArray();

        if (providers.Length == 0)
        {
            throw new InvalidOperationException(
                $"Assistant:Providers must list at least one provider ({AssistantProviders.ZenMux}, {AssistantProviders.Gemini}).");
        }

        if (providers.Contains(AssistantProviders.ZenMux))
        {
            AddZenMuxProvider(services, config);
        }

        if (providers.Contains(AssistantProviders.Gemini))
        {
            AddGeminiProvider(services, config);
        }

        // Failover composite is the single IAssistantChatClient the orchestrator sees. Registering
        // providers as concrete types (not extra IAssistantChatClient registrations) keeps this the
        // only IAssistantChatClient descriptor, so the integration test harness can still swap it.
        services.AddScoped<IAssistantChatClient>(sp =>
        {
            var order = sp.GetRequiredService<IOptions<AssistantOptions>>().Value.Providers
                .Select(NormalizeProvider)
                .Select(name => (name, ResolveProvider(sp, name)))
                .ToList();
            return new FailoverChatClient(order, sp.GetRequiredService<ILogger<FailoverChatClient>>());
        });

        services.AddScoped<IAssistantToolRegistry>(sp =>
            new AssistantToolRegistry(ToolDefinitions.CreateAll(sp.GetRequiredService<ISender>())));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IConversationStore, DbConversationStore>();

        // Two-phase confirmation gate (T4) — a pure, stateless decision function guarding confirm_order.
        services.AddSingleton<ConfirmationGate>();

        // Orchestrator (T5) — drives the LLM ↔ tool loop per chat turn. Scoped: depends on the scoped
        // chat client / tool registry / conversation store.
        services.AddScoped<AssistantOrchestrator>();

        // Request validator (H1) — the controller invokes it explicitly because the chat endpoint calls
        // the orchestrator directly, not via ISender, so the MediatR ValidationBehavior never runs.
        services.AddScoped<IValidator<AssistantChatRequest>, AssistantChatRequestValidator>();

        return services;
    }

    private static void AddZenMuxProvider(IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<ZenMuxOptions>()
            .Bind(config.GetSection("Assistant:ZenMux"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient(ZenMuxHttpClientName, (sp, client) =>
            {
                var timeoutSeconds = sp.GetRequiredService<IOptions<ZenMuxOptions>>().Value.TimeoutSeconds;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            })
            .AddStandardResilienceHandler(options => options.Retry.MaxRetryAttempts = 2);

        services.AddScoped(sp => new ZenMuxChatClient(
            sp.GetRequiredService<IOptions<ZenMuxOptions>>(),
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(ZenMuxHttpClientName)));
    }

    private static void AddGeminiProvider(IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<GeminiOptions>()
            .Bind(config.GetSection("Assistant:Gemini"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Injects a rotating GCP OAuth2 bearer token on every request to the Vertex endpoint.
        services.AddTransient<GcpAuthHandler>();
        services.AddTransient<GeminiThoughtSignatureHandler>();

        services.AddHttpClient(GeminiHttpClientName, (sp, client) =>
            {
                var timeoutSeconds = sp.GetRequiredService<IOptions<GeminiOptions>>().Value.TimeoutSeconds;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            })
            .AddHttpMessageHandler<GcpAuthHandler>()
            .AddHttpMessageHandler<GeminiThoughtSignatureHandler>()
            .AddStandardResilienceHandler(options => options.Retry.MaxRetryAttempts = 2);

        services.AddScoped(sp => new GeminiChatClient(
            sp.GetRequiredService<IOptions<GeminiOptions>>(),
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(GeminiHttpClientName),
            sp.GetRequiredService<ILogger<GeminiChatClient>>()));
    }

    private static IAssistantChatClient ResolveProvider(IServiceProvider sp, string name) => name switch
    {
        AssistantProviders.ZenMux => sp.GetRequiredService<ZenMuxChatClient>(),
        AssistantProviders.Gemini => sp.GetRequiredService<GeminiChatClient>(),
        _ => throw new InvalidOperationException($"Unknown assistant provider '{name}'.")
    };

    private static string NormalizeProvider(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Equals(AssistantProviders.ZenMux, StringComparison.OrdinalIgnoreCase))
        {
            return AssistantProviders.ZenMux;
        }

        if (trimmed.Equals(AssistantProviders.Gemini, StringComparison.OrdinalIgnoreCase))
        {
            return AssistantProviders.Gemini;
        }

        throw new InvalidOperationException(
            $"Unknown assistant provider '{name}'. Valid: {AssistantProviders.ZenMux}, {AssistantProviders.Gemini}.");
    }
}
