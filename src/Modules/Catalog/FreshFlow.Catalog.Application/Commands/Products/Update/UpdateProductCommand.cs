using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Products.Update;

public record UpdateProductCommand(
    Guid Id,
    string Name,
    Guid UnitId,
    Guid? CategoryId,
    string? Description)
    : IRequest<Result<ProductDto>>;
