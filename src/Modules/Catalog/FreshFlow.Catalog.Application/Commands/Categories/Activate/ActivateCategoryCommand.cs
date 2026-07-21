using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Catalog.Application.Commands.Categories.Activate;

public sealed record ActivateCategoryCommand(Guid Id) : ICommand<CategoryDto>;
