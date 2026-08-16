using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Hub.Infrastructure.CrossModule;

internal sealed class HubStaffReader(AppDbContext db) : IHubStaffReader
{
    public async Task<IReadOnlyList<HubStaffUserDto>> GetByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct)
    {
        if (userIds.Count == 0)
            return [];

        return await db.Set<HubStaffUserRow>()
            .AsNoTracking()
            .Where(user => userIds.Contains(user.UserId))
            .Select(user => new HubStaffUserDto(
                user.UserId,
                user.RoleName,
                user.IsActive,
                user.DeletedAt))
            .ToListAsync(ct);
    }
}
