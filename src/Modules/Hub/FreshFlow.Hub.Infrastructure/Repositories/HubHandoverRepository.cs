using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Hub.Infrastructure.Repositories;

internal sealed class HubHandoverRepository(AppDbContext db) : IHubHandoverRepository
{
    public async Task AddAsync(HubHandoverEvent handover, CancellationToken ct) =>
        await db.Set<HubHandoverEvent>().AddAsync(handover, ct);

    public Task<HubHandoverEvent?> FindByIdAsync(Guid hubId, Guid handoverId, CancellationToken ct) =>
        db.Set<HubHandoverEvent>()
            .FirstOrDefaultAsync(h =>
                h.Id == handoverId &&
                h.HubId == hubId &&
                h.DeletedAt == null,
                ct);

    public async Task<(IReadOnlyList<HubHandoverEvent> Items, string? NextCursor)> GetPageAsync(
        Guid hubId,
        string? cursor,
        int pageSize,
        CancellationToken ct)
    {
        if (pageSize <= 0)
            throw new ArgumentException("pageSize must be greater than zero.", nameof(pageSize));

        var query = db.Set<HubHandoverEvent>()
            .AsNoTracking()
            .Where(h => h.HubId == hubId && h.DeletedAt == null);

        var decoded = HubHandoverCursor.TryDecode(cursor);
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
        return (page, HubHandoverCursor.Encode(page[^1].Id, page[^1].CreatedAt));
    }

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);

    private sealed record HubHandoverCursor(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("createdAt")] DateTime CreatedAt)
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static HubHandoverCursor? TryDecode(string? encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return null;

            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return JsonSerializer.Deserialize<HubHandoverCursor>(json, Options);
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
            var json = JsonSerializer.Serialize(new HubHandoverCursor(id, createdAt), Options);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}
