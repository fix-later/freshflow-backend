namespace FreshFlow.Hub.Application.Abstractions;

public sealed class HubConcurrencyException : Exception
{
    public HubConcurrencyException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

public sealed class HubMarketConflictException : Exception
{
    public HubMarketConflictException(Exception inner)
        : base("The market already has an active hub.", inner)
    {
    }
}
