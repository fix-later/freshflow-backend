using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Orders.Infrastructure.Repositories;

internal sealed class OrderClaimRepository(AppDbContext db) : IOrderClaimRepository
{
    public Task<OrderClaim?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<OrderClaim>()
            .FirstOrDefaultAsync(claim => claim.Id == id && claim.DeletedAt == null, ct);

    public async Task<(IReadOnlyList<OrderClaim> Claims, string? NextCursor)> SearchAsync(
        OrderClaimSearchCriteria criteria,
        CancellationToken ct)
    {
        var query = db.Set<OrderClaim>()
            .AsNoTracking()
            .Where(claim => claim.DeletedAt == null);

        if (criteria.RestaurantId.HasValue)
            query = query.Where(claim => claim.RestaurantId == criteria.RestaurantId.Value);
        if (criteria.Status.HasValue)
            query = query.Where(claim => claim.Status == criteria.Status.Value);

        var cursor = ClaimCursor.TryDecode(criteria.Cursor);
        if (cursor is not null)
            query = query.Where(claim =>
                claim.CreatedAt < cursor.CreatedAt
                || claim.CreatedAt == cursor.CreatedAt && claim.Id < cursor.Id);

        var rows = await query
            .OrderByDescending(claim => claim.CreatedAt)
            .ThenByDescending(claim => claim.Id)
            .Take(criteria.PageSize + 1)
            .ToListAsync(ct);

        if (rows.Count <= criteria.PageSize)
            return (rows.AsReadOnly(), null);

        var page = rows.Take(criteria.PageSize).ToList().AsReadOnly();
        return (page, ClaimCursor.Encode(page[^1].Id, page[^1].CreatedAt));
    }

    public async Task AddAsync(OrderClaim claim, CancellationToken ct) =>
        await db.Set<OrderClaim>().AddAsync(claim, ct);

    public void Track(OrderClaim claim)
    {
        var entry = db.Entry(claim);
        if (entry.State == EntityState.Detached)
            db.Attach(claim);

        entry.State = EntityState.Modified;
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private sealed record ClaimCursor(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("createdAt")] DateTime CreatedAt)
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static ClaimCursor? TryDecode(string? encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return null;

            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return JsonSerializer.Deserialize<ClaimCursor>(json, Options);
            }
            catch
            {
                return null;
            }
        }

        public static string Encode(Guid id, DateTime createdAt)
        {
            var json = JsonSerializer.Serialize(new ClaimCursor(id, createdAt), Options);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}
