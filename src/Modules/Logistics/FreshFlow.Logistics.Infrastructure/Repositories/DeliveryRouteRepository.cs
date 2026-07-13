using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshFlow.Logistics.Infrastructure.Repositories;

internal sealed class DeliveryRouteRepository(AppDbContext db) : IDeliveryRouteRepository
{
    public async Task AddAsync(DeliveryRoute route, CancellationToken ct) =>
        await db.Set<DeliveryRoute>().AddAsync(route, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);

    public async Task<bool> SaveAssignmentAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return false;
        }
    }

    public Task<DeliveryRoute?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<DeliveryRoute>().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<DeliveryRoute>> GetByDriverAndDateAsync(
        Guid driverUserId,
        DateOnly serviceDate,
        CancellationToken ct) =>
        await db.Set<DeliveryRoute>()
            .AsNoTracking()
            .Where(r =>
                r.DriverUserId == driverUserId &&
                r.ServiceDate == serviceDate &&
                r.DeletedAt == null)
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);

    public async Task<bool> ExistsOtherRouteForVehicleOnDateAsync(
        Guid vehicleId,
        DateOnly serviceDate,
        Guid excludeRouteId,
        CancellationToken ct) =>
        await db.Set<DeliveryRoute>()
            .AsNoTracking()
            .AnyAsync(
                r =>
                    r.Id != excludeRouteId &&
                    r.VehicleId == vehicleId &&
                    r.ServiceDate == serviceDate &&
                    r.Status != RouteStatus.cancelled &&
                    r.DeletedAt == null,
                ct);

    public async Task<(IReadOnlyList<DeliveryRoute> Items, string? NextCursor)> GetPageAsync(
        string? cursor,
        int pageSize,
        DateOnly? serviceDate,
        RouteStatus? status,
        CancellationToken ct)
    {
        if (pageSize <= 0)
            throw new ArgumentException("pageSize must be greater than zero.", nameof(pageSize));

        var query = db.Set<DeliveryRoute>()
            .AsNoTracking()
            .Where(r => r.DeletedAt == null);

        if (serviceDate.HasValue)
            query = query.Where(r => r.ServiceDate == serviceDate.Value);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var decoded = RouteCursor.TryDecode(cursor);
        if (decoded is not null)
        {
            query = query.Where(r =>
                r.CreatedAt < decoded.CreatedAt ||
                (r.CreatedAt == decoded.CreatedAt && r.Id < decoded.Id));
        }

        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        if (rows.Count <= pageSize)
            return (rows.AsReadOnly(), null);

        var page = rows.Take(pageSize).ToList().AsReadOnly();
        var nextCursor = RouteCursor.Encode(page[^1].Id, page[^1].CreatedAt);
        return (page, nextCursor);
    }

    private sealed record RouteCursor(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("createdAt")] DateTime CreatedAt)
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static RouteCursor? TryDecode(string? encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return null;

            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return JsonSerializer.Deserialize<RouteCursor>(json, Options);
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
            var json = JsonSerializer.Serialize(new RouteCursor(id, createdAt), Options);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }

    internal static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation;
}
