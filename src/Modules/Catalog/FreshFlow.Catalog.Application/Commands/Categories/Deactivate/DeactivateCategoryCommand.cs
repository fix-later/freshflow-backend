using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Catalog.Application.Commands.Categories.Deactivate;

public sealed record DeactivateCategoryCommand(Guid Id) : ICommand<CategoryDto>;
