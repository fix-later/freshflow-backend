using FreshFlow.Auth.Domain.Entities;

namespace FreshFlow.Auth.Application.Abstractions;

public interface IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct);
    public Task AddAsync(RefreshToken token, CancellationToken ct);
    public Task RevokeByFamilyAsync(Guid familyId, CancellationToken ct);
    public Task RevokeByUserAsync(Guid userId, CancellationToken ct);
    public Task SaveChangesAsync(CancellationToken ct);
}
