using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Auth.Infrastructure.Repositories;

internal sealed class VerificationCodeRepository(AppDbContext db) : IVerificationCodeRepository
{
    public Task<VerificationCode?> FindByUserChannelAndHashAsync(
        Guid userId, string channel, string codeHash, CancellationToken ct) =>
        db.Set<VerificationCode>()
            .FirstOrDefaultAsync(v =>
                v.UserId == userId &&
                v.Channel == channel &&
                v.CodeHash == codeHash, ct);

    public async Task InvalidatePendingAsync(Guid userId, string channel, CancellationToken ct)
    {
        var pending = await db.Set<VerificationCode>()
            .Where(v => v.UserId == userId && v.Channel == channel && v.UsedAt == null)
            .ToListAsync(ct);

        foreach (var code in pending)
            code.MarkUsed();
    }

    public Task AddAsync(VerificationCode code, CancellationToken ct) =>
        db.Set<VerificationCode>().AddAsync(code, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);
}
