namespace FreshFlow.Orders.Application.Abstractions;

public sealed class CreditConcurrencyException : Exception
{
    public CreditConcurrencyException()
        : base("The credit account was updated by another request. Please refresh and retry.")
    {
    }

    public CreditConcurrencyException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
