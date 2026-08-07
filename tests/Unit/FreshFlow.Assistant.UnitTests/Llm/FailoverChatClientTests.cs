using FluentAssertions;
using FreshFlow.API.Assistant.Abstractions;
using FreshFlow.API.Assistant.Conversation;
using FreshFlow.API.Assistant.Llm;
using FreshFlow.API.Assistant.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FreshFlow.Assistant.UnitTests.Llm;

public class FailoverChatClientTests
{
    [Fact]
    public async Task Falls_back_to_the_next_provider_when_the_first_runs_out_of_quota()
    {
        // Arrange — primary hits its quota (rate limited), fallback answers.
        var primary = ProviderThatThrows(AssistantProviderFailure.RateLimited);
        var fallback = Substitute.For<IAssistantChatClient>();
        fallback.CompleteAsync(Arg.Any<ConversationState>(), Arg.Any<IReadOnlyList<AssistantTool>>(), Arg.Any<CancellationToken>())
            .Returns(AssistantTurnResult.FromText("từ gemini"));

        var sut = new FailoverChatClient(
            [("ZenMux", primary), ("Gemini", fallback)], NullLogger<FailoverChatClient>.Instance);

        // Act
        var result = await sut.CompleteAsync(State(), []);

        // Assert
        result.Text.Should().Be("từ gemini");
        await fallback.Received(1).CompleteAsync(Arg.Any<ConversationState>(), Arg.Any<IReadOnlyList<AssistantTool>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Uses_the_first_provider_and_never_calls_the_fallback_on_success()
    {
        // Arrange
        var primary = Substitute.For<IAssistantChatClient>();
        primary.CompleteAsync(Arg.Any<ConversationState>(), Arg.Any<IReadOnlyList<AssistantTool>>(), Arg.Any<CancellationToken>())
            .Returns(AssistantTurnResult.FromText("từ zenmux"));
        var fallback = Substitute.For<IAssistantChatClient>();

        var sut = new FailoverChatClient(
            [("ZenMux", primary), ("Gemini", fallback)], NullLogger<FailoverChatClient>.Instance);

        // Act
        var result = await sut.CompleteAsync(State(), []);

        // Assert
        result.Text.Should().Be("từ zenmux");
        await fallback.DidNotReceive().CompleteAsync(Arg.Any<ConversationState>(), Arg.Any<IReadOnlyList<AssistantTool>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_not_fail_over_on_authentication_failure_and_rethrows_immediately()
    {
        // Arrange — a bad key is a config bug; masking it behind a fallback would hide the problem.
        var primary = ProviderThatThrows(AssistantProviderFailure.AuthenticationFailed);
        var fallback = Substitute.For<IAssistantChatClient>();

        var sut = new FailoverChatClient(
            [("ZenMux", primary), ("Gemini", fallback)], NullLogger<FailoverChatClient>.Instance);

        // Act
        var act = () => sut.CompleteAsync(State(), []);

        // Assert
        await act.Should().ThrowAsync<AssistantProviderException>();
        await fallback.DidNotReceive().CompleteAsync(Arg.Any<ConversationState>(), Arg.Any<IReadOnlyList<AssistantTool>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rethrows_the_last_exception_when_every_provider_fails()
    {
        // Arrange
        var primary = ProviderThatThrows(AssistantProviderFailure.RateLimited);
        var fallback = ProviderThatThrows(AssistantProviderFailure.Unavailable);

        var sut = new FailoverChatClient(
            [("ZenMux", primary), ("Gemini", fallback)], NullLogger<FailoverChatClient>.Instance);

        // Act
        var act = () => sut.CompleteAsync(State(), []);

        // Assert
        (await act.Should().ThrowAsync<AssistantProviderException>())
            .Which.Failure.Should().Be(AssistantProviderFailure.Unavailable);
    }

    private static IAssistantChatClient ProviderThatThrows(AssistantProviderFailure failure)
    {
        var client = Substitute.For<IAssistantChatClient>();
        client.CompleteAsync(Arg.Any<ConversationState>(), Arg.Any<IReadOnlyList<AssistantTool>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AssistantProviderException(failure, new InvalidOperationException("boom")));
        return client;
    }

    private static ConversationState State() => new(
        SessionId: "session-1",
        UserId: Guid.NewGuid(),
        MarketId: Guid.NewGuid(),
        Turns: [new ConversationTurn(ConversationRole.User, "chào")]);
}
