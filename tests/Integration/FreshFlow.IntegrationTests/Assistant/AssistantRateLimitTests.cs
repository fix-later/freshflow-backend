using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Assistant;

/// <summary>
/// Verifies the "assistant" rate-limit policy (SCRUM-252) is enforced on
/// <c>POST /api/v1/assistant/chat</c> and that a throttled response returns the standard API error
/// envelope. Uses a factory with PermitLimit = 3 so the 4th request trips the limiter; the scripted
/// LLM keeps each accepted request cheap (no GLM call).
/// </summary>
[Trait("Category", "Integration")]
public sealed class AssistantRateLimitTests(AssistantRateLimitTests.LowLimitFactory factory)
    : IClassFixture<AssistantRateLimitTests.LowLimitFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Assistant factory with a tiny per-window budget so the 4th request triggers 429.</summary>
    public sealed class LowLimitFactory : AssistantWebAppFactory
    {
        protected override int AssistantPermitLimit => 3;
    }

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ChatEndpoint_AfterPermitLimitReached_Returns429WithErrorEnvelope()
    {
        await AuthenticateAsRestaurantAsync();
        object Body() => new { sessionId = Guid.NewGuid().ToString(), message = "xin chào", marketId = (Guid?)null };

        // First 3 requests are within the window.
        for (var i = 1; i <= 3; i++)
        {
            var ok = await _client.PostAsJsonAsync("/api/v1/assistant/chat", Body());
            ok.StatusCode.Should().Be(HttpStatusCode.OK, because: $"request #{i} is within the permit limit");
        }

        // The 4th request must be throttled.
        var blocked = await _client.PostAsJsonAsync("/api/v1/assistant/chat", Body());

        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        var envelope = await blocked.Content.ReadFromJsonAsync<ErrorEnvelope>(JsonOpts);
        envelope!.Success.Should().BeFalse();
        envelope.Error!.Code.Should().Be("TOO_MANY_REQUESTS");
    }

    private async Task AuthenticateAsRestaurantAsync()
    {
        var adminToken = await LoginAsync("admin@assistant.test", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var email = $"assist-rl-{Guid.NewGuid():N}@test.freshflow";
        const string password = "RestaurantP@ss1";
        var createResp = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password,
            role = "restaurant",
            restaurantName = "Assistant RateLimit Restaurant"
        });
        createResp.EnsureSuccessStatusCode();

        var restaurantToken = await LoginAsync(email, password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", restaurantToken);
    }

    private async Task<string> LoginAsync(string identifier, string password)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { identifier, password });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<TokenBody>>(JsonOpts);
        return env!.Data!.AccessToken;
    }
}
