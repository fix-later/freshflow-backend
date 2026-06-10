using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Auth.Infrastructure.Repositories;

internal sealed class UserMarketAssignmentRepository(AppDbContext db) : IUserMarketAssignmentRepository
{
    public async Task AddAsync(UserMarketAssignment assignment, CancellationToken ct) =>
        await db.Set<UserMarketAssignment>().AddAsync(assignment, ct);

    public async Task<IReadOnlyList<UserMarketAssignment>> GetByUserIdAsync(
        Guid userId, CancellationToken ct) =>
        await db.Set<UserMarketAssignment>()
            .Where(a => a.UserId == userId)
            .ToListAsync(ct);

    public Task RemoveRangeAsync(IEnumerable<UserMarketAssignment> assignments, CancellationToken ct)
    {
        db.Set<UserMarketAssignment>().RemoveRange(assignments);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);
}
