namespace FreshFlow.Hub.Application.Abstractions;

public sealed class HubConcurrencyException : Exception
{
    public HubConcurrencyException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
