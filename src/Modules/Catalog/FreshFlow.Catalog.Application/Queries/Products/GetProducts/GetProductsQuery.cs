using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Queries.Products.GetProducts;

public record GetProductsQuery(
    string? Search,
    string? Category,
    bool IncludeInactive,
    int? Page,
    int? PageSize)
    : IRequest<Result<GetProductsResponse>>;
