using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Hub.Infrastructure.Repositories;

internal sealed class CrossDockRepository(AppDbContext db) : ICrossDockRepository
{
    public async Task AddAsync(CrossDockTransfer transfer, CancellationToken ct) =>
        await db.Set<CrossDockTransfer>().AddAsync(transfer, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);

    public async Task<(IReadOnlyList<CrossDockTransfer> Items, string? NextCursor)> GetPageAsync(
        Guid hubId,
        string? status,
        string? cursor,
        int pageSize,
        CancellationToken ct)
    {
        if (pageSize <= 0)
            throw new ArgumentException("pageSize must be greater than zero.", nameof(pageSize));

        var query = db.Set<CrossDockTransfer>()
            .AsNoTracking()
            .Where(t => t.HubId == hubId && t.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(t => t.Status == status);

        var decoded = CrossDockCursor.TryDecode(cursor);
        if (decoded is not null)
        {
            query = query.Where(t =>
                t.CreatedAt < decoded.CreatedAt ||
                (t.CreatedAt == decoded.CreatedAt && t.Id < decoded.Id));
        }

        var rows = await query
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        if (rows.Count <= pageSize)
            return (rows.AsReadOnly(), null);

        var page = rows.Take(pageSize).ToList().AsReadOnly();
        return (page, CrossDockCursor.Encode(page[^1].Id, page[^1].CreatedAt));
    }

    private sealed record CrossDockCursor(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("createdAt")] DateTime CreatedAt)
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static CrossDockCursor? TryDecode(string? encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return null;

            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return JsonSerializer.Deserialize<CrossDockCursor>(json, Options);
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
            var json = JsonSerializer.Serialize(new CrossDockCursor(id, createdAt), Options);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}
