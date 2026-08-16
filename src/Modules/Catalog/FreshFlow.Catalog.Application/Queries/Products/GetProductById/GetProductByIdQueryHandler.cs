using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Queries.Products.GetProductById;

internal sealed class GetProductByIdQueryHandler(IProductRepository products)
    : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken ct)
    {
        var product = await products.FindByIdAsync(request.Id, ct);
        if (product is null)
            return Result<ProductDto>.Failure(Error.NotFound("Product", request.Id));

        return Result<ProductDto>.Success(product.ToDto());
    }
}
