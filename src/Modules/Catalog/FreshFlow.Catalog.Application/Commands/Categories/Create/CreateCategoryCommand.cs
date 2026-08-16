using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Catalog.Application.Commands.Categories.Create;

public sealed record CreateCategoryCommand(
    string Name,
    Guid? ParentId = null,
    string? ImageUrl = null) : ICommand<CategoryDto>;
