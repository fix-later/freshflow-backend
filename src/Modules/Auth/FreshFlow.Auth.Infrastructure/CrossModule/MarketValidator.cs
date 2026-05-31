using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Auth.Infrastructure.CrossModule;

internal sealed class MarketValidator(AppDbContext db) : IMarketValidator
{
    public Task<bool> IsActiveMarketAsync(Guid marketId, CancellationToken ct) =>
        db.Set<MarketRow>()
            .AnyAsync(m => m.Id == marketId && m.IsActive, ct);
}
