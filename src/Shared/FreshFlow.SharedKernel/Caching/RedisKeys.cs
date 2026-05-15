namespace FreshFlow.SharedKernel.Caching;

public static class RedisKeys
{
    public static string Price(Guid marketId, Guid productId) =>
        $"price:{marketId}:{productId}";

    public static string Reservation(Guid marketId, Guid productId) =>
        $"reservation:{marketId}:{productId}";

    public static string Route(string hash) =>
        $"route:{hash}";

    public static string Analytics(string type, string date) =>
        $"analytics:{type}:{date}";
}
