using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HubEntity = FreshFlow.Hub.Domain.Entities.Hub;

namespace FreshFlow.Hub.Infrastructure.Repositories;

internal sealed class HubRepository(AppDbContext db) : IHubRepository
{
    public async Task AddAsync(HubEntity hub, CancellationToken ct) =>
        await db.Set<HubEntity>().AddAsync(hub, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        HubSaveChanges.SaveAsync(db, ct);

    public Task<HubEntity?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<HubEntity>().FirstOrDefaultAsync(h => h.Id == id, ct);

    public Task<bool> HasActiveForMarketAsync(Guid marketId, CancellationToken ct) =>
        db.Set<HubEntity>()
            .AsNoTracking()
            .AnyAsync(h =>
                h.MarketId == marketId &&
                h.IsActive &&
                h.DeletedAt == null,
                ct);

    public Task<bool> HasPendingInboundAsync(Guid hubId, CancellationToken ct) =>
        db.Set<HubInboundEvent>()
            .AsNoTracking()
            .AnyAsync(e =>
                e.HubId == hubId &&
                e.Status == HubInboundEvent.StatusPending &&
                e.DeletedAt == null,
                ct);

    public async Task<(IReadOnlyList<HubEntity> Items, string? NextCursor)> GetPageAsync(
        string? cursor,
        int pageSize,
        bool? isActive,
        CancellationToken ct)
    {
        if (pageSize <= 0)
            throw new ArgumentException("pageSize must be greater than zero.", nameof(pageSize));

        var query = db.Set<HubEntity>().AsNoTracking();

        if (isActive is { } active)
            query = query.Where(h => h.IsActive == active);

        var decoded = HubCursor.TryDecode(cursor);
        if (decoded is not null)
        {
            query = query.Where(h =>
                h.CreatedAt < decoded.CreatedAt ||
                (h.CreatedAt == decoded.CreatedAt && h.Id < decoded.Id));
        }

        var rows = await query
            .OrderByDescending(h => h.CreatedAt)
            .ThenByDescending(h => h.Id)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        if (rows.Count <= pageSize)
            return (rows.AsReadOnly(), null);

        var page = rows.Take(pageSize).ToList().AsReadOnly();
        var nextCursor = HubCursor.Encode(page[^1].Id, page[^1].CreatedAt);
        return (page, nextCursor);
    }

    private sealed record HubCursor(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("createdAt")] DateTime CreatedAt)
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static HubCursor? TryDecode(string? encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return null;

            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return JsonSerializer.Deserialize<HubCursor>(json, Options);
            }
            catch (FormatException)
            {
                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static string Encode(Guid id, DateTime createdAt)
        {
            var json = JsonSerializer.Serialize(new HubCursor(id, createdAt), Options);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}
