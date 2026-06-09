using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;
using MediatR;

namespace FreshFlow.Catalog.Application.Commands.Products.Deactivate;

public record DeactivateProductCommand(Guid Id) : IRequest<Result<ProductDto>>;
