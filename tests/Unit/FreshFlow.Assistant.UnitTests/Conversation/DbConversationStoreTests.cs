using FluentAssertions;
using FreshFlow.API.Assistant.Conversation;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Assistant.UnitTests.Conversation;

[Trait("Category", "Unit")]
public sealed class DbConversationStoreTests
{
    private static AppDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new AppDbContext(options);
    }

    private static DbConversationStore CreateStore(AppDbContext ctx) =>
        new(ctx, TimeProvider.System);

    private static ConversationState SampleState(string sessionId, int turnCount = 1) =>
        new(
            sessionId,
            UserId: Guid.NewGuid(),
            MarketId: Guid.NewGuid(),
            Turns: Enumerable.Range(0, turnCount)
                .Select(i => new ConversationTurn(ConversationRole.User, $"turn-{i}"))
                .ToList(),
            CurrentDraftOrderId: null);

    [Fact]
    public async Task SaveAsync_then_LoadAsync_round_trips_the_full_conversation_state()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var state = SampleState("session-1", turnCount: 3);

        // Act
        await using (var writeCtx = CreateInMemoryContext(dbName))
        {
            var store = CreateStore(writeCtx);
            await store.SaveAsync(state);
        }

        ConversationState? loaded;
        await using (var readCtx = CreateInMemoryContext(dbName))
        {
            var store = CreateStore(readCtx);
            loaded = await store.LoadAsync("session-1");
        }

        // Assert
        loaded.Should().NotBeNull();
        loaded!.SessionId.Should().Be(state.SessionId);
        loaded.UserId.Should().Be(state.UserId);
        loaded.MarketId.Should().Be(state.MarketId);
        loaded.Turns.Should().HaveCount(3);
        loaded.Turns[1].Text.Should().Be("turn-1");
    }

    [Fact]
    public async Task LoadAsync_returns_null_when_no_conversation_exists_for_the_session()
    {
        // Arrange
        await using var ctx = CreateInMemoryContext(Guid.NewGuid().ToString());
        var store = CreateStore(ctx);

        // Act
        var loaded = await store.LoadAsync("does-not-exist");

        // Assert
        loaded.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_returns_null_for_an_expired_conversation_lazy_expire()
    {
        // Arrange — write directly via EF so we control ExpiresAt precisely (in the past).
        var dbName = Guid.NewGuid().ToString();
        var state = SampleState("expired-session");

        await using (var writeCtx = CreateInMemoryContext(dbName))
        {
            var store = CreateStore(writeCtx);
            await store.SaveAsync(state);

            var row = await writeCtx.Set<FreshFlow.Infrastructure.Persistence.Entities.AssistantConversation>()
                .SingleAsync(c => c.SessionId == "expired-session");
            row.Touch(row.StateJson, DateTime.UtcNow.AddMinutes(-31), TimeSpan.FromMinutes(30));
            await writeCtx.SaveChangesAsync();
        }

        // Act
        await using var readCtx = CreateInMemoryContext(dbName);
        var readStore = CreateStore(readCtx);
        var loaded = await readStore.LoadAsync("expired-session");

        // Assert
        loaded.Should().BeNull("an expired row must read back as if it never existed");
    }

    [Fact]
    public async Task SaveAsync_overwrites_an_existing_session_and_slides_the_ttl_forward()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var first = SampleState("session-2", turnCount: 1);

        await using var ctx = CreateInMemoryContext(dbName);
        var store = CreateStore(ctx);
        await store.SaveAsync(first);

        var beforeExpiresAt = (await ctx.Set<FreshFlow.Infrastructure.Persistence.Entities.AssistantConversation>()
            .SingleAsync(c => c.SessionId == "session-2")).ExpiresAt;

        // Act — save again with more turns after a short delay so the TTL clearly moves forward.
        await Task.Delay(10);
        var second = first with { Turns = [.. first.Turns, new ConversationTurn(ConversationRole.Assistant, "reply")] };
        await store.SaveAsync(second);

        var row = await ctx.Set<FreshFlow.Infrastructure.Persistence.Entities.AssistantConversation>()
            .SingleAsync(c => c.SessionId == "session-2");

        // Assert
        row.ExpiresAt.Should().BeAfter(beforeExpiresAt);
        var reloaded = await store.LoadAsync("session-2");
        reloaded!.Turns.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveAsync_trims_history_to_the_most_recent_turns_when_over_the_retention_window()
    {
        // Arrange — more turns than the store keeps. The retention window itself is an
        // implementation detail (DbConversationStore.MaxTurnsToKeep is private); this test only
        // asserts the externally-observable contract: oldest turns are dropped first, recent
        // ones survive in order. A generous overshoot (50) keeps the test valid even if the
        // store's window changes.
        const int turnCount = 50;
        var dbName = Guid.NewGuid().ToString();
        var state = SampleState("session-3", turnCount);

        await using var ctx = CreateInMemoryContext(dbName);
        var store = CreateStore(ctx);

        // Act
        await store.SaveAsync(state);
        var loaded = await store.LoadAsync("session-3");

        // Assert — fewer turns than written (some were trimmed), and the ones kept are the most
        // recent, contiguous, and still in order.
        loaded!.Turns.Should().HaveCountLessThan(turnCount);
        loaded.Turns.Should().NotBeEmpty();
        loaded.Turns[^1].Text.Should().Be($"turn-{turnCount - 1}", "the newest turn must always survive trimming");

        var keptIndex = int.Parse(loaded.Turns[0].Text!["turn-".Length..]);
        for (var i = 0; i < loaded.Turns.Count; i++)
        {
            loaded.Turns[i].Text.Should().Be($"turn-{keptIndex + i}", "kept turns must remain contiguous and in order");
        }
    }

    [Fact]
    public async Task SaveAsync_preserves_the_current_draft_order_id()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var draftOrderId = Guid.NewGuid();
        var state = SampleState("session-4") with { CurrentDraftOrderId = draftOrderId };

        await using var ctx = CreateInMemoryContext(dbName);
        var store = CreateStore(ctx);

        // Act
        await store.SaveAsync(state);
        var loaded = await store.LoadAsync("session-4");

        // Assert
        loaded!.CurrentDraftOrderId.Should().Be(draftOrderId);
    }

    [Fact]
    public async Task SaveAsync_active_session_owned_by_another_user_returns_false_without_overwrite()
    {
        var dbName = Guid.NewGuid().ToString();
        var ownerState = SampleState("owned-session");
        var otherState = ownerState with
        {
            UserId = Guid.NewGuid(),
            Turns = [new ConversationTurn(ConversationRole.User, "intruder")]
        };

        await using var ctx = CreateInMemoryContext(dbName);
        var store = CreateStore(ctx);

        var ownerSaved = await store.SaveAsync(ownerState);
        var otherSaved = await store.SaveAsync(otherState);
        var loaded = await store.LoadAsync(ownerState.SessionId);

        ownerSaved.Should().BeTrue();
        otherSaved.Should().BeFalse();
        loaded!.UserId.Should().Be(ownerState.UserId);
        loaded.Turns[0].Text.Should().Be(ownerState.Turns[0].Text);
    }

    [Fact]
    public async Task SaveAsync_expired_session_cannot_be_reassigned_to_another_user()
    {
        var dbName = Guid.NewGuid().ToString();
        var first = SampleState("reused-session");

        await using var ctx = CreateInMemoryContext(dbName);
        var store = CreateStore(ctx);
        await store.SaveAsync(first);

        var row = await ctx.Set<FreshFlow.Infrastructure.Persistence.Entities.AssistantConversation>()
            .SingleAsync(c => c.SessionId == first.SessionId);
        row.Touch(row.StateJson, DateTime.UtcNow.AddMinutes(-31), TimeSpan.FromMinutes(30));
        await ctx.SaveChangesAsync();

        var replacement = first with
        {
            UserId = Guid.NewGuid(),
            Turns = [new ConversationTurn(ConversationRole.User, "fresh start")]
        };

        var saved = await store.SaveAsync(replacement);

        saved.Should().BeFalse();
        row.UserId.Should().Be(first.UserId);
        row.ExpiresAt.Should().BeBefore(DateTime.UtcNow);
    }
}
