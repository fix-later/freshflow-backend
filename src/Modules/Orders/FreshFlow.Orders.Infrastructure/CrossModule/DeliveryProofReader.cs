using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Orders.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FreshFlow.Orders.Infrastructure.CrossModule;

internal sealed class DeliveryProofReader(AppDbContext db) : IDeliveryProofReader
{
    public Task<string?> FindByOrderIdAsync(Guid orderId, CancellationToken ct) =>
        db.Database.SqlQuery<string>(
                $"""
                 SELECT proof_url AS "Value"
                 FROM deliveries
                 WHERE order_id = {orderId}
                   AND deleted_at IS NULL
                   AND proof_url IS NOT NULL
                 """)
            .SingleOrDefaultAsync(ct);
}
