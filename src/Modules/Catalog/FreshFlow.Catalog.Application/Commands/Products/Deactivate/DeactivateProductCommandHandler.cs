using FreshFlow.Catalog.Application.Abstractions;
using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.Catalog.Application.Mappings;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Products.Deactivate;

internal sealed class DeactivateProductCommandHandler(IProductRepository products, IMarketListingReader listings)
    : IRequestHandler<DeactivateProductCommand, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(DeactivateProductCommand request, CancellationToken ct)
    {
        var product = await products.FindByIdAsync(request.Id, ct);
        if (product is null)
            return Result<ProductDto>.Failure(Error.NotFound("Product", request.Id));

        var activeListings = await listings.CountActiveListingsAsync(request.Id, ct);
        if (activeListings > 0)
            return Result<ProductDto>.Failure(Error.Conflict(
                "PRODUCT_HAS_ACTIVE_LISTINGS",
                $"The product is still listed in {activeListings} market(s). Remove those listings first."));

        product.Delete();

        await products.SaveChangesAsync(ct);

        return Result<ProductDto>.Success(product.ToDto());
    }
}
