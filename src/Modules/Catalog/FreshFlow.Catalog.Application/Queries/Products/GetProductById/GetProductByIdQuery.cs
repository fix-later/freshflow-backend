using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Catalog.Application.Queries.Products.GetProductById;

public sealed record GetProductByIdQuery(Guid Id) : IQuery<ProductDto>;
