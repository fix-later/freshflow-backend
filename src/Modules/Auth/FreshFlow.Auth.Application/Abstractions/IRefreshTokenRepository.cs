using FreshFlow.Auth.Domain.Entities;

namespace FreshFlow.Auth.Application.Abstractions;

public interface IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct);
    public Task AddAsync(RefreshToken token, CancellationToken ct);
    public Task RevokeByFamilyAsync(Guid familyId, string reason, CancellationToken ct);
    public Task RevokeByUserAsync(Guid userId, string reason, CancellationToken ct);
    public Task SaveChangesAsync(CancellationToken ct);
}
