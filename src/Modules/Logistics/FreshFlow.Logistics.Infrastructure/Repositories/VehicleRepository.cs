using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Logistics.Infrastructure.Repositories;

internal sealed class VehicleRepository(AppDbContext db) : IVehicleRepository
{
    public async Task AddAsync(Vehicle vehicle, CancellationToken ct) =>
        await db.Set<Vehicle>().AddAsync(vehicle, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);

    public Task<Vehicle?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<Vehicle>().FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<bool> PlateNumberExistsAsync(
        string plateNumber,
        Guid? excludeId,
        CancellationToken ct)
    {
        var normalized = plateNumber.Trim();
        return db.Set<Vehicle>()
            .AnyAsync(v =>
                v.DeletedAt == null &&
                v.PlateNumber == normalized &&
                (!excludeId.HasValue || v.Id != excludeId.Value),
                ct);
    }

    public async Task<(IReadOnlyList<Vehicle> Items, string? NextCursor)> GetPageAsync(
        string? cursor,
        int pageSize,
        bool? isActive,
        Guid? hubId,
        CancellationToken ct)
    {
        if (pageSize <= 0)
            throw new ArgumentException("pageSize must be greater than zero.", nameof(pageSize));

        var query = db.Set<Vehicle>().AsNoTracking();

        if (isActive.HasValue)
        {
            query = isActive.Value
                ? query.Where(v => v.DeletedAt == null)
                : query.Where(v => v.DeletedAt != null);
        }

        if (hubId.HasValue)
            query = query.Where(v => v.HubId == hubId.Value);

        var decoded = VehicleCursor.TryDecode(cursor);
        if (decoded is not null)
        {
            query = query.Where(v =>
                v.CreatedAt < decoded.CreatedAt ||
                (v.CreatedAt == decoded.CreatedAt && v.Id < decoded.Id));
        }

        var rows = await query
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.Id)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        if (rows.Count <= pageSize)
            return (rows.AsReadOnly(), null);

        var page = rows.Take(pageSize).ToList().AsReadOnly();
        var nextCursor = VehicleCursor.Encode(page[^1].Id, page[^1].CreatedAt);
        return (page, nextCursor);
    }

    private sealed record VehicleCursor(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("createdAt")] DateTime CreatedAt)
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static VehicleCursor? TryDecode(string? encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return null;

            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                return JsonSerializer.Deserialize<VehicleCursor>(json, Options);
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
            var json = JsonSerializer.Serialize(new VehicleCursor(id, createdAt), Options);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }
    }
}
