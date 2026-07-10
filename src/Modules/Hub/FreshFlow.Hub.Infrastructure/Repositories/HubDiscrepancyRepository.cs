using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Hub.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Hub.Infrastructure.Repositories;

internal sealed class HubDiscrepancyRepository(AppDbContext db)
    : IHubDiscrepancyRepository, IHubDiscrepancyReader
{
    public async Task AddAsync(HubDiscrepancy discrepancy, CancellationToken ct) =>
        await db.Set<HubDiscrepancy>().AddAsync(discrepancy, ct);

    public Task<HubDiscrepancy?> FindByIdForHubAsync(Guid hubId, Guid discrepancyId, CancellationToken ct) =>
        db.Set<HubDiscrepancy>()
            .FirstOrDefaultAsync(d =>
                d.Id == discrepancyId &&
                d.HubId == hubId &&
                d.DeletedAt == null,
                ct);

    public async Task<(IReadOnlyList<HubDiscrepancy> Items, string? NextCursor)> GetPageAsync(
        Guid hubId,
        string? status,
        string? cursor,
        int pageSize,
        CancellationToken ct)
    {
        if (pageSize <= 0)
            throw new ArgumentException("pageSize must be greater than zero.", nameof(pageSize));

        var query = db.Set<HubDiscrepancy>()
            .AsNoTracking()
            .Where(d => d.HubId == hubId && d.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(d => d.Status == status);

        var decoded = HubDiscrepancyCursor.TryDecode(cursor);
        if (decoded is not null)
        {
            query = query.Where(d =>
                d.CreatedAt < decoded.CreatedAt ||
                (d.CreatedAt == decoded.CreatedAt && d.Id < decoded.Id));
        }

        var rows = await query
            .OrderByDescending(d => d.CreatedAt)
            .ThenByDescending(d => d.Id)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        if (rows.Count <= pageSize)
            return (rows.AsReadOnly(), null);

        var page = rows.Take(pageSize).ToList().AsReadOnly();
        return (page, HubDiscrepancyCursor.Encode(page[^1].Id, page[^1].CreatedAt));
    }

    public Task<bool> HasOpenDiscrepanciesForOrderAsync(Guid orderId, CancellationToken ct) =>
        db.Set<HubDiscrepancy>()
            .AsNoTracking()
            .AnyAsync(d =>
                d.OrderId == orderId &&
                d.Status == HubDiscrepancy.StatusOpen &&
                d.DeletedAt == null,
                ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);

    private sealed record HubDiscrepancyCursor(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("createdAt")] DateTime CreatedAt)
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static HubDiscrepancyCursor? TryDecode(string? encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return null;

            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return JsonSerializer.Deserialize<HubDiscrepancyCursor>(json, Options);
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
            var json = JsonSerializer.Serialize(new HubDiscrepancyCursor(id, createdAt), Options);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}
