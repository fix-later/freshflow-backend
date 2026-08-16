namespace FreshFlow.Orders.Application.Abstractions;

public sealed class DuplicateCreditSettlementException : Exception
{
    public DuplicateCreditSettlementException(Exception inner)
        : base("A settlement with this reference already exists for the restaurant.", inner)
    {
    }
}
