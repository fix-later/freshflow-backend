using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using FreshFlow.API.Assistant.Llm;

namespace FreshFlow.Assistant.UnitTests.Llm;

public class GeminiThoughtSignatureHandlerTests
{
    [Fact]
    public async Task SendAsync_preserves_captured_signatures_and_bypasses_unknown_provider_calls()
    {
        var inner = new StubHandler();
        using var client = new HttpClient(new GeminiThoughtSignatureHandler { InnerHandler = inner });

        await client.PostAsync("https://example.test/chat/completions", JsonContent("{}"));
        await client.PostAsync("https://example.test/chat/completions", JsonContent(RequestWithToolCalls));

        var messages = JsonNode.Parse(inner.RequestBodies[1])!["messages"]!.AsArray();
        Signature(messages[0]!).Should().Be("real-signature");
        Signature(messages[1]!).Should().Be(GeminiThoughtSignatureHandler.BypassSignature);
    }

    private static StringContent JsonContent(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static string? Signature(JsonNode message) =>
        message["tool_calls"]?[0]?["extra_content"]?["google"]?["thought_signature"]?.GetValue<string>();

    private const string RequestWithToolCalls = """
        {
          "messages": [
            {
              "role": "assistant",
              "tool_calls": [{"id":"call-1","type":"function","function":{"name":"search_products","arguments":"{}"}}]
            },
            {
              "role": "assistant",
              "tool_calls": [{"id":"call-from-fallback","type":"function","function":{"name":"search_products","arguments":"{}"}}]
            }
          ]
        }
        """;

    private sealed class StubHandler : HttpMessageHandler
    {
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            var body = RequestBodies.Count == 1
                ? """{"choices":[{"message":{"tool_calls":[{"id":"call-1","extra_content":{"google":{"thought_signature":"real-signature"}}}]}}]}"""
                : "{}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent(body) };
        }
    }
}
