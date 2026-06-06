using FreshFlow.Auth.Domain.Entities;

namespace FreshFlow.Auth.Application.Abstractions;

public interface IRoleRepository
{
    public Task<Role?> FindByNameAsync(string name, CancellationToken ct);
    public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct);
}
