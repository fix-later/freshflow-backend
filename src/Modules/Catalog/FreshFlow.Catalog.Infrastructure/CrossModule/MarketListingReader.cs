using System.Data;
using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FreshFlow.Catalog.Infrastructure.CrossModule;

/// <summary>
/// Reads Pricing's <c>market_products</c> table to guard product deactivation (C6). A single
/// COUNT does not warrant a keyless EF Row — that would enter <c>AppDbContextModelSnapshot</c>
/// and force a migration against a live database for no schema change. Follows the ADO pattern
/// established by <c>Orders.Infrastructure/CrossModule/MarketSessionGate.cs</c> instead.
/// </summary>
internal sealed class MarketListingReader(AppDbContext db) : IMarketListingReader
{
    public async Task<int> CountActiveListingsAsync(Guid productId, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT COUNT(*) FROM market_products
                WHERE "ProductId" = @product_id AND deleted_at IS NULL
                """;
            var product = command.CreateParameter();
            product.ParameterName = "product_id";
            product.Value = productId;
            command.Parameters.Add(product);

            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt32(result);
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }
    }
}
