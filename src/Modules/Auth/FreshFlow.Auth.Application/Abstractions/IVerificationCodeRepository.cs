using FreshFlow.Auth.Domain.Entities;

namespace FreshFlow.Auth.Application.Abstractions;

public interface IVerificationCodeRepository
{
    Task<VerificationCode?> FindByUserChannelAndHashAsync(Guid userId, string channel, string codeHash, CancellationToken ct);
    Task InvalidatePendingAsync(Guid userId, string channel, CancellationToken ct);
    Task AddAsync(VerificationCode code, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
