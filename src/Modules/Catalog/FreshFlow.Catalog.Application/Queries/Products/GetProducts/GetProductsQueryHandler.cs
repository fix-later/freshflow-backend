using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Queries.Products.GetProducts;

internal sealed class GetProductsQueryHandler(IProductRepository products)
    : IRequestHandler<GetProductsQuery, Result<GetProductsResponse>>
{
    public async Task<Result<GetProductsResponse>> Handle(GetProductsQuery request, CancellationToken ct)
    {
        var (items, total) = await products.GetPagedAsync(
            request.Search,
            request.Category,
            request.IncludeInactive,
            request.Page,
            request.PageSize,
            ct);

        var dtos = items.Select(p => p.ToDto()).ToList().AsReadOnly();
        var meta = new PaginationMeta(total, request.Page, request.PageSize);

        return Result<GetProductsResponse>.Success(new GetProductsResponse(dtos, meta));
    }
}
