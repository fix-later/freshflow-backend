using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Catalog.Application.Commands.Categories.Update;

public sealed record UpdateCategoryCommand(Guid Id, string Name, Guid? ParentId = null) : ICommand<CategoryDto>;
