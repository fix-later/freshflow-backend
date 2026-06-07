using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Aggregates;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Auth.Infrastructure.Repositories;

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken ct) =>
        db.Set<User>()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<User>()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> FindByIdentifierAsync(string identifier, CancellationToken ct)
    {
        var normalized = identifier.Trim().ToLowerInvariant();
        return db.Set<User>()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == normalized || u.Phone == normalized, ct);
    }

    public Task<bool> ExistsAsync(string email, CancellationToken ct) =>
        db.Set<User>().AnyAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public Task<bool> ExistsByPhoneAsync(string phone, CancellationToken ct)
    {
        var normalized = phone.Trim().ToLowerInvariant();
        return db.Set<User>().AnyAsync(u => u.Phone == normalized, ct);
    }

    public async Task AddAsync(User user, CancellationToken ct)
    {
        // If Role navigation is set but not yet tracked by EF (e.g. loaded in another scope),
        // attach it as Unchanged so EF does not attempt a duplicate INSERT into roles.
        if (db.Entry(user.Role).State == EntityState.Detached)
            db.Attach(user.Role);

        await db.Set<User>().AddAsync(user, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);

    public async Task<(IReadOnlyList<User> Data, int Total)> GetPagedAsync(
        string? role, bool? isActive, string? search,
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.Set<User>().Include(u => u.Role).AsQueryable();

        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(u => u.Role.Name == role.ToLowerInvariant());

        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(u => u.Email.Contains(s) || (u.Phone != null && u.Phone.Contains(s)));
        }

        var total = await query.CountAsync(ct);
        var data = await query
            .OrderBy(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (data, total);
    }
}
