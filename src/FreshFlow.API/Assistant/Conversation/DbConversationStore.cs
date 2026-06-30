using System.Text.Json;
using FreshFlow.API.Assistant.Abstractions;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.API.Assistant.Conversation;

/// <summary>
/// Postgres-backed <see cref="IConversationStore"/> (T3) on top of the shared <see cref="AppDbContext"/>.
/// Conversations are append/overwrite rows keyed by <c>session_id</c> — there is no separate create vs.
/// update path, <see cref="SaveAsync"/> always upserts. Expiry is lazy: a row past its TTL is treated as
/// absent on read, no background sweep job is required for v1.
/// </summary>
public sealed class DbConversationStore(AppDbContext dbContext, TimeProvider timeProvider) : IConversationStore
{
    /// <summary>Conversation TTL, slid forward on every <see cref="SaveAsync"/>.</summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    /// <summary>Most recent turns kept per session — older history is dropped before persisting.</summary>
    public const int MaxRetainedTurns = 20;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<ConversationState?> LoadAsync(string sessionId, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var row = await dbContext.Set<AssistantConversation>()
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.SessionId == sessionId, ct);

        // Lazy-expire: an expired row reads back as if it were never there. No delete here — the
        // next SaveAsync for this session overwrites it, and an unused expired row is harmless.
        if (row is null || row.ExpiresAt <= now)
        {
            return null;
        }

        return JsonSerializer.Deserialize<ConversationState>(row.StateJson, SerializerOptions);
    }

    public async Task SaveAsync(ConversationState state, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var trimmed = TrimHistory(state);
        var stateJson = JsonSerializer.Serialize(trimmed, SerializerOptions);

        var row = await dbContext.Set<AssistantConversation>()
            .SingleOrDefaultAsync(c => c.SessionId == state.SessionId, ct);

        if (row is null)
        {
            dbContext.Set<AssistantConversation>().Add(new AssistantConversation(
                id: Guid.NewGuid(),
                sessionId: state.SessionId,
                userId: state.UserId,
                marketId: state.MarketId,
                stateJson: stateJson,
                createdAt: now,
                updatedAt: now,
                expiresAt: now + Ttl));
        }
        else
        {
            row.Touch(stateJson, now, Ttl);
        }

        await dbContext.SaveChangesAsync(ct);
    }

    /// <summary>Keeps only the most recent <see cref="MaxRetainedTurns"/> turns — bounds row size over a long session.</summary>
    private static ConversationState TrimHistory(ConversationState state)
    {
        if (state.Turns.Count <= MaxRetainedTurns)
        {
            return state;
        }

        var recentTurns = state.Turns.Skip(state.Turns.Count - MaxRetainedTurns).ToList();
        return state with { Turns = recentTurns };
    }
}
