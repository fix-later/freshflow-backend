using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Auth.Infrastructure.Repositories;

internal sealed class PasswordResetTokenRepository(AppDbContext db) : IPasswordResetTokenRepository
{
    public Task<PasswordResetToken?> FindLatestPendingByUserIdAsync(Guid userId, CancellationToken ct) =>
        db.Set<PasswordResetToken>()
            .Where(t => t.UserId == userId && t.UsedAt == null)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task InvalidatePendingAsync(Guid userId, CancellationToken ct) =>
        await db.Set<PasswordResetToken>()
            .Where(t => t.UserId == userId
                        && t.UsedAt == null
                        && t.ExpiresAt > DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, DateTime.UtcNow), ct);

    public async Task AddAsync(PasswordResetToken token, CancellationToken ct) =>
        await db.Set<PasswordResetToken>().AddAsync(token, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);
}
