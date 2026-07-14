using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Procurement.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Procurement.Infrastructure.CrossModule;

internal sealed class OperationalSettingsReader(AppDbContext db) : IOperationalSettingsReader
{
    public async Task<ProcurementOperationalSettingsDto> ReadAsync(CancellationToken ct)
    {
        var row = await db.Set<OperationalSettingsRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(ct);

        return row is null
            ? new ProcurementOperationalSettingsDto(true, new TimeOnly(22, 0))
            : new ProcurementOperationalSettingsDto(row.BatchingEnabled, row.DailyCutoffTime);
    }
}
