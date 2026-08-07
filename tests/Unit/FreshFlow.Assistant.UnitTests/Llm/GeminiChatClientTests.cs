using System.Text.Json;
using FluentAssertions;
using FreshFlow.API.Assistant.Conversation;
using FreshFlow.API.Assistant.Llm;
using FreshFlow.API.Assistant.Tools;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace FreshFlow.Assistant.UnitTests.Llm;

public class GeminiChatClientTests
{
    private static readonly GeminiOptions Options = new()
    {
        ProjectId = "freshflow-xxxxx",
        Location = "asia-southeast1",
        Model = "google/gemini-2.5-flash"
    };

    [Fact]
    public async Task CompleteAsync_returns_text_when_provider_replies_without_a_tool_call()
    {
        // Arrange
        var inner = Substitute.For<IChatClient>();
        inner.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Xin chào, tôi có thể giúp gì?")));

        var sut = new GeminiChatClient(Options, inner);
        var state = StateWithUserMessage("chào bạn");

        // Act
        var result = await sut.CompleteAsync(state, []);

        // Assert
        result.IsToolCall.Should().BeFalse();
        result.Text.Should().Be("Xin chào, tôi có thể giúp gì?");
    }

    [Fact]
    public async Task CompleteAsync_returns_a_tool_call_when_provider_requests_one()
    {
        // Arrange
        var arguments = new Dictionary<string, object?> { ["searchText"] = "cà chua" };
        var functionCall = new FunctionCallContent("call-1", "search_products", arguments);
        var inner = Substitute.For<IChatClient>();
        inner.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, [functionCall])));

        var sut = new GeminiChatClient(Options, inner);
        var state = StateWithUserMessage("tìm cà chua giúp tôi");

        // Act
        var result = await sut.CompleteAsync(state, []);

        // Assert
        result.IsToolCall.Should().BeTrue();
        result.ToolCallId.Should().Be("call-1");
        result.ToolName.Should().Be("search_products");
        var parsedArgs = JsonSerializer.Deserialize<Dictionary<string, object?>>(result.ToolArgsJson!);
        parsedArgs.Should().ContainKey("searchText");
    }

    [Fact]
    public async Task CompleteAsync_forwards_tool_declarations_to_the_provider_as_chat_options_tools()
    {
        // Arrange
        ChatOptions? capturedOptions = null;
        var inner = Substitute.For<IChatClient>();
        inner.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedOptions = callInfo.ArgAt<ChatOptions>(1);
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
            });

        var sut = new GeminiChatClient(Options, inner);
        var schema = JsonDocument.Parse("""{"type":"object","properties":{"searchText":{"type":"string"}}}""").RootElement;
        var tools = new[] { new AssistantTool("search_products", "Tìm sản phẩm theo tên", schema) };

        // Act
        await sut.CompleteAsync(StateWithUserMessage("tìm giúp tôi"), tools);

        // Assert
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Tools.Should().ContainSingle(t => t.Name == "search_products");
    }

    [Fact]
    public async Task CompleteAsync_maps_conversation_history_roles_in_order()
    {
        // Arrange
        IEnumerable<ChatMessage>? capturedMessages = null;
        var inner = Substitute.For<IChatClient>();
        inner.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedMessages = callInfo.ArgAt<IEnumerable<ChatMessage>>(0);
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
            });

        var sut = new GeminiChatClient(Options, inner);
        var state = new ConversationState(
            SessionId: "session-1",
            UserId: Guid.NewGuid(),
            MarketId: Guid.NewGuid(),
            Turns:
            [
                new ConversationTurn(ConversationRole.User, "tìm cà chua"),
                new ConversationTurn(ConversationRole.Assistant, null, ToolCallId: "call-1", ToolName: "search_products", ToolCallArgsJson: """{"searchText":"cà chua"}"""),
                new ConversationTurn(ConversationRole.Tool, null, ToolCallId: "call-1", ToolResultJson: """{"items":[]}""")
            ]);

        // Act
        await sut.CompleteAsync(state, []);

        // Assert
        var roles = capturedMessages!.Select(m => m.Role).ToList();
        roles.Should().Equal(ChatRole.User, ChatRole.Assistant, ChatRole.Tool);
    }

    private static ConversationState StateWithUserMessage(string text) => new(
        SessionId: "session-1",
        UserId: Guid.NewGuid(),
        MarketId: Guid.NewGuid(),
        Turns: [new ConversationTurn(ConversationRole.User, text)]);
}
