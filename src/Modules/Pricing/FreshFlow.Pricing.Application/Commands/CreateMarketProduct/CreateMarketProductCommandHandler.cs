using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.Pricing.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Pricing.Application.Commands.CreateMarketProduct;

/// <summary>
/// SCRUM-178 — Add Product to Market.
///
/// Error precedence (cheapest/most authoritative first):
/// 422 business-rule    → initialPrice &lt;= 0, initialQuantity &lt; 0
/// 404 market missing   → market does not exist / inactive
/// 404 product missing  → catalog product does not exist
/// 409 conflict          → product already listed at this market
/// 201 success
/// </summary>
internal sealed class CreateMarketProductCommandHandler(
    IMarketProductRepository marketProductRepository,
    IMarketProductReader marketProductReader)
    : IRequestHandler<CreateMarketProductCommand, Result<CreateMarketProductResultDto>>
{
    public async Task<Result<CreateMarketProductResultDto>> Handle(
        CreateMarketProductCommand request, CancellationToken cancellationToken)
    {
        // ── 1. Business-rule validation (422) — no DB round-trip ──────────────
        if (request.InitialPrice <= 0)
            return Result<CreateMarketProductResultDto>.Failure(
                Error.Validation("INVALID_PRICE", "Initial price must be greater than 0."));

        if (request.InitialQuantity < 0)
            return Result<CreateMarketProductResultDto>.Failure(
                Error.Validation("INVALID_QUANTITY", "Initial quantity must be non-negative."));

        // ── 2. Market existence check (404) ───────────────────────────────────
        var marketExists = await marketProductReader.MarketExistsAsync(
            request.MarketId, cancellationToken);

        if (!marketExists)
            return Result<CreateMarketProductResultDto>.Failure(
                Error.NotFound("MARKET", request.MarketId));

        // ── 3. Product existence check (404) ──────────────────────────────────
        var productExists = await marketProductReader.ProductExistsAsync(
            request.ProductId, cancellationToken);

        if (!productExists)
            return Result<CreateMarketProductResultDto>.Failure(
                Error.NotFound("PRODUCT", request.ProductId));

        // ── 4. Duplicate listing guard (409) ───────────────────────────────────
        var existing = await marketProductRepository.FindByMarketAndProductAsync(
            request.MarketId, request.ProductId, cancellationToken);

        if (existing is not null)
            return Result<CreateMarketProductResultDto>.Failure(
                Error.Conflict(
                    "MARKET_PRODUCT_ALREADY_EXISTS",
                    "This product is already listed at this market."));

        // ── 5. Create and persist ──────────────────────────────────────────────
        var marketProduct = new MarketProduct(
            request.MarketId,
            request.ProductId,
            request.InitialPrice,
            request.InitialQuantity,
            request.CreatedBy);

        await marketProductRepository.AddAsync(marketProduct, cancellationToken);
        await marketProductRepository.SaveChangesAsync(cancellationToken);

        // ── 6. Build result ─────────────────────────────────────────────────────
        return Result<CreateMarketProductResultDto>.Success(new CreateMarketProductResultDto(
            MarketProductId: marketProduct.Id,
            MarketId: marketProduct.MarketId,
            ProductId: marketProduct.ProductId,
            CurrentPrice: marketProduct.CurrentPrice,
            CurrentQuantity: marketProduct.CurrentQuantity,
            CreatedAt: marketProduct.CreatedAt,
            CreatedBy: marketProduct.UpdatedBy));
    }
}
