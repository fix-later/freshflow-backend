using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FreshFlow.API.Assistant.Abstractions;
using FreshFlow.IntegrationTests.Infrastructure;

namespace FreshFlow.IntegrationTests.Assistant;

/// <summary>
/// Integration coverage for <c>POST /api/v1/assistant/chat</c> (SCRUM-251 / SCRUM-253) over the full
/// HTTP → controller → orchestrator → tool registry → confirmation gate → conversation-store path. The
/// LLM is the scripted <see cref="ScriptedAssistantChatClient"/>, so no real GLM call is made.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AssistantChatEndpointTests(AssistantWebAppFactory factory)
    : IClassFixture<AssistantWebAppFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly AssistantWebAppFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Chat_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsJsonAsync(
            "/api/v1/assistant/chat",
            new { sessionId = Guid.NewGuid().ToString(), message = "xin chào", marketId = (Guid?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Chat_AsRestaurant_WithDirectTextReply_Returns200AndEchoesReply()
    {
        // Arrange — the LLM answers directly with no tool call.
        _factory.ChatClient.Reset();
        _factory.ChatClient.Enqueue(AssistantTurnResult.FromText("Chào bạn! Bạn muốn đặt món gì hôm nay?"));
        await AuthenticateAsRestaurantAsync();
        var sessionId = Guid.NewGuid().ToString();

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/v1/assistant/chat",
            new { sessionId, message = "xin chào", marketId = Guid.NewGuid() });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Envelope<ChatData>>(JsonOpts);
        body!.Success.Should().BeTrue();
        body.Data!.Reply.Should().Be("Chào bạn! Bạn muốn đặt món gì hôm nay?");
        body.Data.SessionId.Should().Be(sessionId);
        body.Data.PendingConfirmation.Should().BeNull();
    }

    [Fact]
    public async Task Chat_WhenLlmTriesToConfirmWithoutClientFlag_BlocksConfirmAndReturnsPendingConfirmation()
    {
        // Arrange — the LLM requests confirm_order, but the client sent no confirmOrderId flag. The
        // two-phase gate must withhold the confirmation and surface a pendingConfirmation instead.
        var orderId = Guid.NewGuid();
        _factory.ChatClient.Reset();
        _factory.ChatClient.Enqueue(
            AssistantTurnResult.FromToolCall("call-1", "confirm_order", JsonSerializer.Serialize(new { orderId })));
        await AuthenticateAsRestaurantAsync();

        // Act — no confirmOrderId in the request body.
        var response = await _client.PostAsJsonAsync(
            "/api/v1/assistant/chat",
            new { sessionId = Guid.NewGuid().ToString(), message = "xác nhận đơn giúp tôi", marketId = (Guid?)null });

        // Assert — request succeeds, but confirmation is pending (never auto-confirmed).
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Envelope<ChatData>>(JsonOpts);
        body!.Data!.PendingConfirmation.Should().NotBeNull();
        body.Data.PendingConfirmation!.OrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task Chat_WithEmptyMessage_Returns400()
    {
        // Arrange — H1: request validation runs before anything else; the LLM is never called.
        await AuthenticateAsRestaurantAsync();

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/v1/assistant/chat",
            new { sessionId = Guid.NewGuid().ToString(), message = "   ", marketId = (Guid?)null });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Chat_WhenSessionIdBelongsToAnotherUser_ReturnsNotFound()
    {
        // Arrange — C1 (CWE-639): User A starts a conversation under a client-chosen sessionId.
        _factory.ChatClient.Reset();
        _factory.ChatClient.Enqueue(AssistantTurnResult.FromText("Đơn nháp của bạn đã sẵn sàng."));
        var sessionId = Guid.NewGuid().ToString();

        var tokenA = await CreateRestaurantAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var aResponse = await _client.PostAsJsonAsync(
            "/api/v1/assistant/chat",
            new { sessionId, message = "tạo đơn giúp tôi", marketId = Guid.NewGuid() });
        aResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act — a DIFFERENT authenticated user (B) tries to resume A's sessionId.
        var tokenB = await CreateRestaurantAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        var bResponse = await _client.PostAsJsonAsync(
            "/api/v1/assistant/chat",
            new { sessionId, message = "cho tôi xem đơn", marketId = (Guid?)null });

        // Assert — B must not be able to resume A's session; 404 hides its existence entirely.
        bResponse.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            because: "a conversation owned by another user must not be resumable (CWE-639)");
    }

    private async Task AuthenticateAsRestaurantAsync()
    {
        var token = await CreateRestaurantAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>Creates a fresh, active restaurant user via the admin API and returns its access token.</summary>
    private async Task<string> CreateRestaurantAndGetTokenAsync()
    {
        var adminToken = await LoginAsync("admin@assistant.test", "AdminP@ss1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var email = $"assist-rest-{Guid.NewGuid():N}@test.freshflow";
        const string password = "RestaurantP@ss1";
        var createResp = await _client.PostAsJsonAsync("/api/v1/admin/users", new
        {
            email,
            password,
            role = "restaurant",
            restaurantName = "Assistant Test Restaurant"
        });
        createResp.EnsureSuccessStatusCode();

        return await LoginAsync(email, password);
    }

    private async Task<string> LoginAsync(string identifier, string password)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { identifier, password });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<Envelope<TokenBody>>(JsonOpts);
        return env!.Data!.AccessToken;
    }

    private sealed record ChatData(
        string Reply,
        string SessionId,
        PendingConfirmationBody? PendingConfirmation,
        Guid? DraftOrderId);

    private sealed record PendingConfirmationBody(Guid OrderId, string PreviewJson);
}
