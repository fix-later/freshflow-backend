namespace FreshFlow.Orders.Application.Dtos;

/// <summary>Item line requested by the client at draft-creation time, before server-side enrichment.</summary>
public sealed record DraftOrderItemRequest(Guid MarketProductId, int Quantity);
