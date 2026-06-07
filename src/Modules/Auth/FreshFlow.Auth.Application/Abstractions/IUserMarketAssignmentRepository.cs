using FreshFlow.Auth.Domain.Entities;

namespace FreshFlow.Auth.Application.Abstractions;

public interface IUserMarketAssignmentRepository
{
    public Task AddAsync(UserMarketAssignment assignment, CancellationToken ct);
}
