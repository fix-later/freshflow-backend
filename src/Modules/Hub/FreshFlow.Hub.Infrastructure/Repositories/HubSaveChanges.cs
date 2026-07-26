using FreshFlow.Hub.Application.Abstractions;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshFlow.Hub.Infrastructure.Repositories;

internal static class HubSaveChanges
{
    public static async Task SaveAsync(AppDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new HubConcurrencyException(
                "Hub data was updated by another request. Please refresh and retry.", ex);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_hubs_active_market"
            })
        {
            throw new HubMarketConflictException(ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
                                          pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new HubConcurrencyException(
                "Hub data was updated by another request. Please refresh and retry.", ex);
        }
    }
}
