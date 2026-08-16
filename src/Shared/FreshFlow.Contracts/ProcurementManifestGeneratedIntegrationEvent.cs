using MediatR;

namespace FreshFlow.Contracts;

public sealed record ProcurementManifestGeneratedIntegrationEvent(
    Guid BatchId,
    Guid MarketId,
    DateOnly BatchDate,
    DateTime ManifestedAt) : INotification;
