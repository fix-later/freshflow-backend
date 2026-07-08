using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Domain.Entities;
using FreshFlow.Orders.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshFlow.Orders.Infrastructure.Repositories;

internal sealed class CreditRepository(AppDbContext db) : ICreditRepository
{
    public async Task<RestaurantCredit?> FindAccountAsync(Guid restaurantId, CancellationToken ct) =>
        await db.Set<RestaurantCredit>()
            .FirstOrDefaultAsync(c => c.RestaurantId == restaurantId, ct);

    public async Task AddAccountAsync(RestaurantCredit account, CancellationToken ct) =>
        await db.Set<RestaurantCredit>().AddAsync(account, ct);

    public void Track(RestaurantCredit account)
    {
        var entry = db.Entry(account);
        if (entry.State == EntityState.Detached)
        {
            db.Attach(account);
            entry = db.Entry(account);
        }

        if (entry.State != EntityState.Added)
            entry.State = EntityState.Modified;
    }

    public void AddTransaction(CreditTransaction transaction) =>
        db.Set<CreditTransaction>().Add(transaction);

    // Balance-moving types only — Adjustment (credit-limit changes) is not a balance
    // movement and must never appear in the ledger, even if legacy rows exist.
    private static readonly CreditTransactionType[] BalanceMovingTypes =
    [
        CreditTransactionType.Charge,
        CreditTransactionType.Settlement,
        CreditTransactionType.Refund,
    ];

    public async Task<(IReadOnlyList<CreditTransaction> Items, string? NextCursor)> GetTransactionsPageAsync(
        Guid restaurantId,
        string? cursor,
        int pageSize,
        DateTime? from,
        DateTime? to,
        CancellationToken ct)
    {
        // Guard: pageSize=0 causes Take(0) → empty page, then page[^1] throws
        // IndexOutOfRangeException when building the next cursor.
        if (pageSize <= 0)
            throw new ArgumentException("pageSize must be greater than zero.", nameof(pageSize));

        var query = db.Set<CreditTransaction>()
            .AsNoTracking()
            .Where(t => t.RestaurantId == restaurantId)
            .Where(t => BalanceMovingTypes.Contains(t.Type));

        // Optional date range filter (inclusive on both ends).
        if (from.HasValue) query = query.Where(t => t.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(t => t.CreatedAt <= to.Value);

        // Composite cursor: (CreatedAt DESC, Id DESC).
        // The predicate mirrors the ORDER BY exactly so that same-millisecond concurrent
        // writes are handled correctly:
        //   CreatedAt < cursor.CreatedAt
        //   OR (CreatedAt == cursor.CreatedAt AND Id < cursor.Id)
        var decoded = TransactionCursor.TryDecode(cursor);
        if (decoded is not null)
            query = query.Where(t =>
                t.CreatedAt < decoded.CreatedAt ||
                (t.CreatedAt == decoded.CreatedAt && t.Id < decoded.Id));

        var rows = await query
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        if (rows.Count <= pageSize)
            return (rows.AsReadOnly(), null);

        var page = rows.Take(pageSize).ToList().AsReadOnly();
        var nextCursor = TransactionCursor.Encode(page[^1].Id, page[^1].CreatedAt);
        return (page, nextCursor);
    }

    public async Task<IReadOnlyList<CreditTransaction>> GetTransactionsInPeriodAsync(
        Guid restaurantId, DateTime from, DateTime toExclusive, CancellationToken ct) =>
        await db.Set<CreditTransaction>()
            .AsNoTracking()
            .Where(t => t.RestaurantId == restaurantId)
            .Where(t => BalanceMovingTypes.Contains(t.Type))
            .Where(t => t.CreatedAt >= from && t.CreatedAt < toExclusive)
            .OrderBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);

    public async Task<decimal> GetNetBalanceMovementBeforeAsync(
        Guid restaurantId, DateTime beforeExclusive, CancellationToken ct) =>
        await db.Set<CreditTransaction>()
            .AsNoTracking()
            .Where(t => t.RestaurantId == restaurantId)
            .Where(t => BalanceMovingTypes.Contains(t.Type))
            .Where(t => t.CreatedAt < beforeExclusive)
            .SumAsync(t => t.Type == CreditTransactionType.Charge ? t.Amount : -t.Amount, ct);

    public async Task<IReadOnlyList<Guid>> GetActiveRestaurantIdsAsync(CancellationToken ct) =>
        await db.Set<RestaurantCredit>()
            .AsNoTracking()
            .Select(c => c.RestaurantId)
            .ToListAsync(ct);

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new CreditConcurrencyException(
                "The credit account was updated by another request. Please refresh and retry.", ex);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new CreditConcurrencyException(
                "The credit account was created by another request. Please refresh and retry.", ex);
        }
    }

    internal static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation;

    // ── Cursor helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Composite cursor following the same base64-JSON pattern as
    /// <c>PriceSnapshotRepository.SnapshotCursor</c>. Encodes <c>(Id, CreatedAt)</c>
    /// of the last item returned on the previous page.
    /// </summary>
    private sealed record TransactionCursor(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("createdAt")] DateTime CreatedAt)
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static TransactionCursor? TryDecode(string? encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return null;
            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return JsonSerializer.Deserialize<TransactionCursor>(json, Options);
            }
            catch
            {
                return null; // invalid cursor treated as start of list
            }
        }

        public static string Encode(Guid id, DateTime createdAt)
        {
            var json = JsonSerializer.Serialize(new TransactionCursor(id, createdAt), Options);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}
