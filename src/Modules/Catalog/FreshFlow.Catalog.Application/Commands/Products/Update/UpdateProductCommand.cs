using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Products.Update;

public record UpdateProductCommand(
    Guid Id,
    string Name,
    Guid UnitId,
    Guid? CategoryId,
    string? Description,
    string? ImageUrl = null,
    Guid? PackingCodeId = null,
    string? VatRate = null,
    int MinimumOrderQuantity = 1)
    : IRequest<Result<ProductDto>>;
