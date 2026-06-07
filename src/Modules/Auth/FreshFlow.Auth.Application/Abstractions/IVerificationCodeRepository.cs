using FreshFlow.Auth.Domain.Entities;

namespace FreshFlow.Auth.Application.Abstractions;

public interface IVerificationCodeRepository
{
    public Task<VerificationCode?> FindByUserChannelAndHashAsync(Guid userId, string channel, string codeHash, CancellationToken ct);
    public Task InvalidatePendingAsync(Guid userId, string channel, CancellationToken ct);
    public Task AddAsync(VerificationCode code, CancellationToken ct);
    public Task SaveChangesAsync(CancellationToken ct);
}
