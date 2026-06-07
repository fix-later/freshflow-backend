using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;

namespace FreshFlow.Auth.Infrastructure.Repositories;

internal sealed class UserMarketAssignmentRepository(AppDbContext db) : IUserMarketAssignmentRepository
{
    public async Task AddAsync(UserMarketAssignment assignment, CancellationToken ct) =>
        await db.Set<UserMarketAssignment>().AddAsync(assignment, ct);
}
