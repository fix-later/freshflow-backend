using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Pricing.Application.Queries.GetPriceChangeHistory;

internal sealed class GetPriceChangeHistoryQueryHandler(
    IMarketProductReader marketReader,
    IMarketProductRepository marketProductRepo,
    IPriceSnapshotRepository snapshotRepo)
    : IRequestHandler<GetPriceChangeHistoryQuery, Result<PriceHistoryPageDto>>
{
    public async Task<Result<PriceHistoryPageDto>> Handle(
        GetPriceChangeHistoryQuery request, CancellationToken ct)
    {
        // 1. Verify market exists — returns 404 MARKET_NOT_FOUND if missing.
        if (!await marketReader.MarketExistsAsync(request.MarketId, ct))
            return Result<PriceHistoryPageDto>.Failure(
                Error.NotFound("MARKET", request.MarketId));

        // 2. Resolve market product — returns 404 PRODUCT_NOT_FOUND if not listed at this market.
        var marketProduct = await marketProductRepo.FindByMarketAndProductAsync(
            request.MarketId, request.ProductId, ct);
        if (marketProduct is null)
            return Result<PriceHistoryPageDto>.Failure(
                Error.NotFound("PRODUCT", request.ProductId));

        // 3. Paginate snapshots with optional date range.
        var (snapshots, nextCursor) = await snapshotRepo.GetPageAsync(
            marketProduct.Id,
            request.Cursor,
            request.PageSize,
            request.From,
            request.To,
            ct);

        var items = snapshots
            .Select(s => new PriceHistoryItemDto(
                s.Id,
                s.MarketProductId,
                s.Price,
                s.Quantity,
                s.RecordedBy,
                s.RecordedAt))
            .ToList()
            .AsReadOnly();

        return Result<PriceHistoryPageDto>.Success(
            new PriceHistoryPageDto(items, request.PageSize, nextCursor));
    }
}
