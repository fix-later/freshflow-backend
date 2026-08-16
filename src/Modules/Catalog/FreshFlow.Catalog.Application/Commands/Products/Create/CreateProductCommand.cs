using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Products.Create;

public record CreateProductCommand(
    string Name,
    Guid UnitId,
    Guid? CategoryId,
    string? Description,
    Guid? CreatedBy,
    Guid? PackingCodeId = null,
    string? VatRate = null,
    int MinimumOrderQuantity = 1)
    : IRequest<Result<ProductDto>>;
