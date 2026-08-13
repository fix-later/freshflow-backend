using System.Data;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

namespace FreshFlow.Orders.Infrastructure.CrossModule;

internal sealed class MarketSessionGate(AppDbContext db, IConfiguration configuration) : IMarketSessionGate
{
    public async Task<MarketSessionGateResult> CheckAsync(
        Guid? marketId, DateOnly serviceDate, bool lockForConfirmation, CancellationToken ct)
    {
        if (!configuration.GetValue("Orders:MarketSessions:Enforce", true))
            return new MarketSessionGateResult(true, true);
        if (!marketId.HasValue)
            return new MarketSessionGateResult(false, false);

        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT status
                FROM market_sessions
                WHERE market_id = @market_id AND service_date = @service_date
                  AND deleted_at IS NULL
                """ + (lockForConfirmation ? " FOR SHARE" : string.Empty);
            var market = command.CreateParameter();
            market.ParameterName = "market_id";
            market.Value = marketId.Value;
            command.Parameters.Add(market);
            var date = command.CreateParameter();
            date.ParameterName = "service_date";
            date.Value = serviceDate;
            command.Parameters.Add(date);
            var status = await command.ExecuteScalarAsync(ct) as string;
            return new MarketSessionGateResult(status is not null, status == "Open");
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }
    }
}
