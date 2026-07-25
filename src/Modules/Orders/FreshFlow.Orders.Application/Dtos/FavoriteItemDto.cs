namespace FreshFlow.Orders.Application.Dtos;

/// <summary>Enriched favorite list item — the FE card renders this without another round trip.</summary>
public sealed record FavoriteItemDto(
    Guid MarketProductId,
    Guid ProductId,
    string ProductName,
    string? ImageUrl,
    Guid MarketId,
    string MarketName,
    string? Category,
    string Unit,
    decimal CurrentPrice,
    int AvailableQuantity,
    DateTime CreatedAt);
