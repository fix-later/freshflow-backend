namespace FreshFlow.Pricing.Application.Abstractions;

/// <summary>
/// Raised by the Infrastructure repository when a DB-level optimistic concurrency
/// conflict is detected (e.g. EF Core's DbUpdateConcurrencyException).
/// Kept in Application so command handlers can catch it without an EF Core dependency.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("The record was modified concurrently. Please refresh and retry.")
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
