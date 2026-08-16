using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Catalog.Application.Commands.Markets.Delete;

public sealed record DeleteMarketCommand(Guid Id) : ICommand;
