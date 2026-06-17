namespace FreshFlow.Pricing.Application.Abstractions;

/// <summary>
/// Raised by the Infrastructure repository when a concurrent insert hits the
/// partial unique index on (market_id, product_id) where deleted_at IS NULL
/// (Postgres SQLSTATE 23505). Kept in Application so command handlers can catch
/// it without any EF Core / Npgsql dependency.
/// </summary>
public sealed class DuplicateMarketProductException : Exception
{
    public DuplicateMarketProductException()
        : base("This product is already listed at this market.")
    {
    }

    public DuplicateMarketProductException(string message)
        : base(message)
    {
    }

    public DuplicateMarketProductException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
