using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.Pricing.Domain.Entities;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Pricing.Application.Commands.UpdateAvailableQuantity;

/// <summary>
/// UC-PRI-04 — Update Available Procurement Quantity.
///
/// Error precedence (cheapest/most authoritative first):
/// 422 business-rule  → quantity &lt; 0
/// 404 market missing → market does not exist / inactive
/// 403 access denied  → agent not assigned to this market
/// 404 product missing → market_product row not found
/// 409 concurrency    → expectedVersion mismatch
/// 200 success        → raises PriceUpdatedDomainEvent for downstream handlers
/// </summary>
internal sealed class UpdateAvailableQuantityCommandHandler(
    IMarketProductRepository marketProductRepository,
    IPriceSnapshotRepository snapshotRepository,
    IAssignedMarketReader assignedMarketReader,
    IMarketProductReader marketProductReader)
    : IRequestHandler<UpdateAvailableQuantityCommand, Result<UpdateAvailableQuantityResultDto>>
{
    public async Task<Result<UpdateAvailableQuantityResultDto>> Handle(
        UpdateAvailableQuantityCommand request, CancellationToken cancellationToken)
    {
        // ── 1. Business-rule validation (422) — no DB round-trip ──────────────
        if (request.Quantity < 0)
            return Result<UpdateAvailableQuantityResultDto>.Failure(
                Error.Validation("INVALID_QUANTITY", "Quantity must be non-negative."));

        // ── 2. Market existence check (404) ───────────────────────────────────
        var marketExists = await marketProductReader.MarketExistsAsync(
            request.MarketId, cancellationToken);

        if (!marketExists)
            return Result<UpdateAvailableQuantityResultDto>.Failure(
                Error.NotFound("MARKET", request.MarketId));

        // ── 3. Agent assignment guard (403) ───────────────────────────────────
        var isAssigned = await assignedMarketReader.HasAssignmentAsync(
            request.AgentUserId, request.MarketId, cancellationToken);

        if (!isAssigned)
            return Result<UpdateAvailableQuantityResultDto>.Failure(
                Error.Unauthorized("MARKET_ACCESS_DENIED",
                    "The market agent is not assigned to this market."));

        // ── 4. Load market product (404) ──────────────────────────────────────
        var mp = await marketProductRepository.FindByMarketAndProductAsync(
            request.MarketId, request.ProductId, cancellationToken);

        if (mp is null)
            return Result<UpdateAvailableQuantityResultDto>.Failure(
                Error.NotFound("PRODUCT", request.ProductId));

        // ── 5. Optimistic concurrency check (409) ─────────────────────────────
        if (request.ExpectedVersion.HasValue &&
            !IsVersionMatch(mp.UpdatedAt, request.ExpectedVersion.Value))
            return Result<UpdateAvailableQuantityResultDto>.Failure(
                Error.Conflict(
                    "OPTIMISTIC_CONCURRENCY_CONFLICT",
                    "The record was updated by another user. Please refresh and retry."));

        // ── 6. Apply domain update (raises PriceUpdatedDomainEvent) ───────────
        var previousQuantity = mp.CurrentQuantity;
        marketProductRepository.Track(mp);
        mp.UpdateAvailableQuantity(request.Quantity, request.AgentUserId);

        // ── 7. Create immutable price snapshot (FR-PRI-004) ───────────────────
        // Snapshot records state after every price OR quantity change so that
        // the price-history feed (UC-PRI-06) has a full audit trail.
        var snapshot = new PriceSnapshot(
            mp.Id, mp.CurrentPrice, mp.CurrentQuantity, request.AgentUserId);

        await snapshotRepository.AddAsync(snapshot, cancellationToken);

        // ── 8. Persist (single unit-of-work; same AppDbContext) ───────────────
        await marketProductRepository.SaveChangesAsync(cancellationToken);

        // ── 9. Build result ───────────────────────────────────────────────────
        return Result<UpdateAvailableQuantityResultDto>.Success(
            new UpdateAvailableQuantityResultDto(
                MarketProductId: mp.Id,
                ProductId: mp.ProductId,
                MarketId: mp.MarketId,
                PreviousQuantity: previousQuantity,
                CurrentQuantity: mp.CurrentQuantity,
                IsOutOfStock: mp.IsOutOfStock,
                UpdatedAt: mp.UpdatedAt,
                UpdatedBy: mp.UpdatedBy));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsVersionMatch(DateTime current, DateTime expected) =>
        Truncate(current) == Truncate(expected);

    private static DateTime Truncate(DateTime dt) =>
        new(dt.Ticks - dt.Ticks % TimeSpan.TicksPerMillisecond, dt.Kind);
}
