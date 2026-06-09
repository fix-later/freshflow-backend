using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Catalog.Application.Commands.Categories.Create;

public sealed record CreateCategoryCommand(string Name) : ICommand<CategoryDto>;
