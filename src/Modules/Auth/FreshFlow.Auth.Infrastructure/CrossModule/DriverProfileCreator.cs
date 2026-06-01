using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Domain.Entities;
using FreshFlow.Infrastructure.Persistence;

namespace FreshFlow.Auth.Infrastructure.CrossModule;

internal sealed class DriverProfileCreator(AppDbContext db) : IDriverProfileCreator
{
    public async Task CreateAsync(Guid userId, CancellationToken ct)
    {
        var profile = new DriverProfile(userId);
        db.Set<DriverProfile>().Add(profile);
        await db.SaveChangesAsync(ct);
    }
}
