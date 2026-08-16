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
        var enforce = configuration.GetValue("Orders:MarketSessions:Enforce", true);
        if (!marketId.HasValue)
            return new MarketSessionGateResult(!enforce, !enforce);

        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT ms.id, ms.status, ms.planned_capacity_kg,
                       COALESCE((
                           SELECT SUM(oi."Quantity")
                           FROM orders o
                           JOIN order_items oi ON oi."OrderId" = o."Id"
                           WHERE o.market_session_id = ms.id
                             AND o.deleted_at IS NULL
                             AND o."Status" <> 'Cancelled'
                       ), 0) AS confirmed_goods_kg
                FROM market_sessions ms
                WHERE ms.market_id = @market_id AND ms.service_date = @service_date
                  AND ms.deleted_at IS NULL
                """ + (lockForConfirmation ? " FOR SHARE OF ms" : string.Empty);
            var market = command.CreateParameter();
            market.ParameterName = "market_id";
            market.Value = marketId.Value;
            command.Parameters.Add(market);
            var date = command.CreateParameter();
            date.ParameterName = "service_date";
            date.Value = serviceDate;
            command.Parameters.Add(date);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return new MarketSessionGateResult(!enforce, !enforce);

            var sessionId = reader.GetGuid(0);
            var status = reader.GetString(1);
            decimal? plannedCapacityKg = reader.IsDBNull(2) ? null : reader.GetDecimal(2);
            var confirmedGoodsKg = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3);
            return new MarketSessionGateResult(
                true, !enforce || status == "Open", sessionId, plannedCapacityKg, confirmedGoodsKg);
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }
    }
}
