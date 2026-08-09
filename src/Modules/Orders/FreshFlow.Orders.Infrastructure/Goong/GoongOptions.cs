using System.ComponentModel.DataAnnotations;

namespace FreshFlow.Orders.Infrastructure.Goong;

internal sealed class GoongOptions
{
    public const string SectionName = "Delivery:Goong";

    [Required, Url]
    public string BaseUrl { get; init; } = "https://rsapi.goong.io";

    public string ApiKey { get; init; } = string.Empty;

    [Required]
    public string Vehicle { get; init; } = "car";

    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 10;

    [Range(0, 5)]
    public int RetryCount { get; init; } = 2;

    [Range(1, 3)]
    public double FallbackRoadFactor { get; init; } = 1.4;
}
