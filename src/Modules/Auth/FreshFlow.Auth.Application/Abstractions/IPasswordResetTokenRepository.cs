using FreshFlow.Auth.Domain.Entities;

namespace FreshFlow.Auth.Application.Abstractions;

public interface IPasswordResetTokenRepository
{
    public Task<PasswordResetToken?> FindByHashAsync(string tokenHash, CancellationToken ct);

    /// <summary>
    /// Marks all pending (non-expired, non-used) reset tokens for the given user as used,
    /// so that issuing a new token invalidates all prior ones.
    /// </summary>
    public Task InvalidatePendingAsync(Guid userId, CancellationToken ct);

    public Task AddAsync(PasswordResetToken token, CancellationToken ct);
    public Task SaveChangesAsync(CancellationToken ct);
}
