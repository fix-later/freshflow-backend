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
/// Wires the assistant's LLM client (T1), tool registry (T2) and conversation store (T3). Safety
/// gate (T4) and the controller/rate-limit/orchestrator wiring (T5/T6) extend this method as
/// their tasks land — kept minimal here to avoid merge collisions across the task split.
/// </summary>
public static class DependencyInjection
{
    private const string ZenMuxHttpClientName = "Assistant.ZenMux";

    public static IServiceCollection AddAssistant(this IServiceCollection services, IConfiguration config)
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
            .AddStandardResilienceHandler(options =>
            {
                // 2 retries with exponential backoff for 5xx/timeout/429 — the standard handler
                // already classifies HttpRequestException, TimeoutRejectedException and
                // TooManyRequests/5xx status codes as transient.
                options.Retry.MaxRetryAttempts = 2;
            });

        services.AddScoped<IAssistantChatClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var options = sp.GetRequiredService<IOptions<ZenMuxOptions>>();
            return new ZenMuxChatClient(options, httpClientFactory.CreateClient(ZenMuxHttpClientName));
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
}
