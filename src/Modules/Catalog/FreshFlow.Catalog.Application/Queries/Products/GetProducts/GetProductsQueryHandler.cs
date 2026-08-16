using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Queries.Products.GetProducts;

internal sealed class GetProductsQueryHandler(IProductRepository products)
    : IRequestHandler<GetProductsQuery, Result<GetProductsResponse>>
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;

    public async Task<Result<GetProductsResponse>> Handle(GetProductsQuery request, CancellationToken ct)
    {
        var page = request.Page ?? DefaultPage;
        var pageSize = request.PageSize ?? DefaultPageSize;

        var (items, total) = await products.GetPagedAsync(
            request.Search,
            request.Category,
            request.IncludeInactive,
            page,
            pageSize,
            ct);

        var dtos = items.Select(p => p.ToDto()).ToList().AsReadOnly();
        var meta = new PaginationMeta(total, page, pageSize);

        return Result<GetProductsResponse>.Success(new GetProductsResponse(dtos, meta));
    }
}
