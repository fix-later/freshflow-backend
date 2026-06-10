using FreshFlow.Auth.Domain.Entities;

namespace FreshFlow.Auth.Application.Abstractions;

public interface IUserMarketAssignmentRepository
{
    public Task AddAsync(UserMarketAssignment assignment, CancellationToken ct);
    public Task<IReadOnlyList<UserMarketAssignment>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    public Task RemoveRangeAsync(IEnumerable<UserMarketAssignment> assignments, CancellationToken ct);
    public Task SaveChangesAsync(CancellationToken ct);
}
