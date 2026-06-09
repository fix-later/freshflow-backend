using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Catalog.Application.Queries.Categories.GetCategories;

public sealed record GetCategoriesQuery(bool ActiveOnly = true) : IQuery<IReadOnlyList<CategoryDto>>;
