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
/// 403 access denied  → agent not assigned to this market (admins bypass)
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

        // ── 3. Agent assignment guard (403) — admins are not assigned to markets ──
        var isAssigned = request.IsAdmin || await assignedMarketReader.HasAssignmentAsync(
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
        // PriceSnapshot.For is the single source of truth for snapshot construction.
        var snapshot = PriceSnapshot.For(mp, request.AgentUserId);
        await snapshotRepository.AddAsync(snapshot, cancellationToken);

        // ── 8. Persist (single unit-of-work; same AppDbContext) ───────────────
        try
        {
            await marketProductRepository.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<UpdateAvailableQuantityResultDto>.Failure(
                Error.Conflict(
                    "OPTIMISTIC_CONCURRENCY_CONFLICT",
                    "The record was updated by another user. Please refresh and retry."));
        }

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

    private static DateTime Truncate(DateTime dt)
    {
        // Strip DateTimeKind and compare raw ticks.
        //
        // All UpdatedAt values in this system originate from DateTime.UtcNow (EF Core returns
        // them with Kind=Utc). Clients may echo the value back with different Kind labels:
        //   - Kind.Utc       → JSON with "Z" suffix (standard path)
        //   - Kind.Unspecified → JSON without timezone suffix (common serialiser default)
        //   - Kind.Local     → SpecifyKind relabeling (no tick shift, same instant)
        //
        // In all three cases the tick count represents the same UTC instant, so raw-tick
        // comparison is correct and avoids timezone-offset shifts from ToUniversalTime().
        var ticks = dt.Ticks;
        return new DateTime(ticks - ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
    }
}
