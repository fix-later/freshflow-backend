using FreshFlow.Catalog.Application.Dtos;

namespace FreshFlow.Catalog.Application.Queries.Products.GetProducts;

public record GetProductsResponse(IReadOnlyList<ProductDto> Data, PaginationMeta Meta);

public record PaginationMeta(int Total, int Page, int PageSize);
