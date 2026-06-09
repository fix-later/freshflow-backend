using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Products.Deactivate;

internal sealed class DeactivateProductCommandHandler(IProductRepository products)
    : IRequestHandler<DeactivateProductCommand, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(DeactivateProductCommand request, CancellationToken ct)
    {
        var product = await products.FindByIdAsync(request.Id, ct);
        if (product is null)
            return Result<ProductDto>.Failure(Error.NotFound("Product", request.Id));

        product.Delete();

        await products.SaveChangesAsync(ct);

        return Result<ProductDto>.Success(product.ToDto());
    }
}
