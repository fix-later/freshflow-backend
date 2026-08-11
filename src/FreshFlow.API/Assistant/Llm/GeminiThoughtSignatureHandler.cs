using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FreshFlow.API.Assistant.Llm;

/// <summary>
/// Preserves Gemini thought signatures across OpenAI-compatible function-call round trips.
/// </summary>
internal sealed class GeminiThoughtSignatureHandler : DelegatingHandler
{
    internal const string BypassSignature = "skip_thought_signature_validator";

    private readonly ConcurrentDictionary<string, string> _signatures = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await InjectSignaturesAsync(request, cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);
        await CaptureSignaturesAsync(response, cancellationToken);
        return response;
    }

    private async Task InjectSignaturesAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Content is null)
        {
            return;
        }

        var originalContent = request.Content;
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(await originalContent.ReadAsByteArrayAsync(ct));
        }
        catch (JsonException)
        {
            return;
        }

        if (root?["messages"] is not JsonArray messages)
        {
            return;
        }

        var changed = false;
        foreach (var message in messages)
        {
            if (message?["tool_calls"] is not JsonArray toolCalls)
            {
                continue;
            }

            foreach (var toolCall in toolCalls)
            {
                if (toolCall is not JsonObject call ||
                    call["extra_content"]?["google"]?["thought_signature"] is not null)
                {
                    continue;
                }

                var callId = call["id"]?.GetValue<string>();
                var signature = callId is not null && _signatures.TryGetValue(callId, out var captured)
                    ? captured
                    : BypassSignature;
                var extraContent = call["extra_content"] as JsonObject ?? [];
                var google = extraContent["google"] as JsonObject ?? [];
                google["thought_signature"] = signature;
                extraContent["google"] = google;
                call["extra_content"] = extraContent;
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        var replacement = new ByteArrayContent(Encoding.UTF8.GetBytes(root.ToJsonString()));
        foreach (var header in originalContent.Headers.Where(h =>
                     !h.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)))
        {
            replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        request.Content = replacement;
        originalContent.Dispose();
    }

    private async Task CaptureSignaturesAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode || response.Content is null)
        {
            return;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(await response.Content.ReadAsByteArrayAsync(ct));
        }
        catch (JsonException)
        {
            return;
        }

        if (root?["choices"] is not JsonArray choices)
        {
            return;
        }

        foreach (var choice in choices)
        {
            if (choice?["message"]?["tool_calls"] is not JsonArray toolCalls)
            {
                continue;
            }

            foreach (var toolCall in toolCalls)
            {
                var callId = toolCall?["id"]?.GetValue<string>();
                var signature = toolCall?["extra_content"]?["google"]?["thought_signature"]?.GetValue<string>();
                if (callId is not null && signature is not null)
                {
                    _signatures[callId] = signature;
                }
            }
        }
    }
}
