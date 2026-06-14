using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.Pricing.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Pricing.Application.Commands.UpdateProductPrice;

/// <summary>
/// UC-PRI-03 — Update Product Price.
///
/// Error precedence (cheapest/most authoritative first):
/// 422 business-rule  → price ≤ 0, quantity &lt; 0
/// 404 market missing → market does not exist / inactive
/// 403 access denied  → agent not assigned to this market
/// 404 product missing → market_product row not found
/// 409 concurrency    → expectedVersion mismatch
/// 200 success
/// </summary>
internal sealed class UpdateProductPriceCommandHandler(
    IMarketProductRepository marketProductRepository,
    IPriceSnapshotRepository snapshotRepository,
    IAssignedMarketReader assignedMarketReader,
    IMarketProductReader marketProductReader)
    : IRequestHandler<UpdateProductPriceCommand, Result<UpdateProductPriceResultDto>>
{
    public async Task<Result<UpdateProductPriceResultDto>> Handle(
        UpdateProductPriceCommand request, CancellationToken cancellationToken)
    {
        // ── 1. Business-rule validation (422) — no DB round-trip ──────────────
        if (request.Price.HasValue && request.Price.Value <= 0)
            return Result<UpdateProductPriceResultDto>.Failure(
                Error.Validation("INVALID_PRICE", "Price must be greater than 0."));

        if (request.Quantity.HasValue && request.Quantity.Value < 0)
            return Result<UpdateProductPriceResultDto>.Failure(
                Error.Validation("INVALID_QUANTITY", "Quantity must be non-negative."));

        // ── 2. Market existence check (404) ───────────────────────────────────
        var marketExists = await marketProductReader.MarketExistsAsync(
            request.MarketId, cancellationToken);

        if (!marketExists)
            return Result<UpdateProductPriceResultDto>.Failure(
                Error.NotFound("MARKET", request.MarketId));

        // ── 3. Agent assignment guard (403) ───────────────────────────────────
        var isAssigned = await assignedMarketReader.HasAssignmentAsync(
            request.AgentUserId, request.MarketId, cancellationToken);

        if (!isAssigned)
            return Result<UpdateProductPriceResultDto>.Failure(
                Error.Unauthorized("MARKET_ACCESS_DENIED",
                    "The market agent is not assigned to this market."));

        // ── 4. Load market product (404) ──────────────────────────────────────
        var mp = await marketProductRepository.FindByMarketAndProductAsync(
            request.MarketId, request.ProductId, cancellationToken);

        if (mp is null)
            return Result<UpdateProductPriceResultDto>.Failure(
                Error.NotFound("PRODUCT", request.ProductId));

        // ── 5. Optimistic concurrency check (409) ─────────────────────────────
        if (request.ExpectedVersion.HasValue &&
            !IsVersionMatch(mp.UpdatedAt, request.ExpectedVersion.Value))
            return Result<UpdateProductPriceResultDto>.Failure(
                Error.Conflict(
                    "OPTIMISTIC_CONCURRENCY_CONFLICT",
                    "The record was updated by another user. Please refresh and retry."));

        // ── 6. Apply domain update (raises PriceUpdatedDomainEvent) ───────────
        var previousPrice = mp.CurrentPrice;
        marketProductRepository.Track(mp);
        mp.ApplyUpdate(request.Price, request.Quantity, request.AgentUserId);

        // ── 7. Create immutable price snapshot (FR-PRI-004) ───────────────────
        // PriceSnapshot.For is the single source of truth for snapshot construction.
        var snapshot = PriceSnapshot.For(mp, request.AgentUserId);
        await snapshotRepository.AddAsync(snapshot, cancellationToken);

        // ── 8. Persist (single unit-of-work; same AppDbContext) ───────────────
        await marketProductRepository.SaveChangesAsync(cancellationToken);

        // ── 9. Build result ───────────────────────────────────────────────────
        var changePercent = CalculateChangePercent(previousPrice, mp.CurrentPrice);

        return Result<UpdateProductPriceResultDto>.Success(new UpdateProductPriceResultDto(
            MarketProductId: mp.Id,
            ProductId: mp.ProductId,
            MarketId: mp.MarketId,
            PreviousPrice: previousPrice,
            CurrentPrice: mp.CurrentPrice,
            CurrentQuantity: mp.CurrentQuantity,
            ChangePercent: changePercent,
            UpdatedAt: mp.UpdatedAt,
            UpdatedBy: mp.UpdatedBy,
            SnapshotId: snapshot.Id));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Compares two DateTime values with millisecond precision to absorb DB rounding.
    /// </summary>
    private static bool IsVersionMatch(DateTime current, DateTime expected) =>
        Truncate(current) == Truncate(expected);

    private static DateTime Truncate(DateTime dt) =>
        new(dt.Ticks - dt.Ticks % TimeSpan.TicksPerMillisecond, dt.Kind);

    private static decimal CalculateChangePercent(decimal previousPrice, decimal currentPrice)
    {
        if (previousPrice == 0m) return 0m;
        return Math.Round((currentPrice - previousPrice) / previousPrice * 100m, 2);
    }
}
