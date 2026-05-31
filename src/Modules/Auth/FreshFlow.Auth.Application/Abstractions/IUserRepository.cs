using FreshFlow.Auth.Domain.Aggregates;

namespace FreshFlow.Auth.Application.Abstractions;

public interface IUserRepository
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken ct);
    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct);
    public Task<bool> ExistsAsync(string email, CancellationToken ct);
    public Task AddAsync(User user, CancellationToken ct);
    public Task SaveChangesAsync(CancellationToken ct);
    public Task<(IReadOnlyList<User> Data, int Total)> GetPagedAsync(
        string? role, bool? isActive, string? search,
        int page, int pageSize, CancellationToken ct);
}
