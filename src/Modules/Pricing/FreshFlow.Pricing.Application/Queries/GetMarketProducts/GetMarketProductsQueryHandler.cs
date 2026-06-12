using FreshFlow.Pricing.Application.Abstractions;
using FreshFlow.Pricing.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Pricing.Application.Queries.GetMarketProducts;

internal sealed class GetMarketProductsQueryHandler(IMarketProductReader reader)
    : IRequestHandler<GetMarketProductsQuery, Result<MarketProductPageDto>>
{
    public async Task<Result<MarketProductPageDto>> Handle(
        GetMarketProductsQuery request, CancellationToken ct)
    {
        if (!await reader.MarketExistsAsync(request.MarketId, ct))
            return Result<MarketProductPageDto>.Failure(
                Error.NotFound("MARKET", request.MarketId));

        var (items, nextCursor) = await reader.GetPageAsync(
            request.MarketId,
            request.Category,
            request.Cursor,
            request.PageSize,
            ct);

        return Result<MarketProductPageDto>.Success(
            new MarketProductPageDto(items, request.PageSize, nextCursor));
    }
}
