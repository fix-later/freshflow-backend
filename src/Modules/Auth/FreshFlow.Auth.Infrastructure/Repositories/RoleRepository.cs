using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Auth.Infrastructure.Repositories;

internal sealed class RoleRepository(AppDbContext db) : IRoleRepository
{
    public Task<Role?> FindByNameAsync(string name, CancellationToken ct) =>
        db.Set<Role>().FirstOrDefaultAsync(r => r.Name == name.ToLowerInvariant(), ct);

    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct) =>
        await db.Set<Role>().OrderBy(r => r.Name).ToListAsync(ct);
}
