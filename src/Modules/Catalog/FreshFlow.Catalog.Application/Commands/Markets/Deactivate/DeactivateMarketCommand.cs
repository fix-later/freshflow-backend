using FreshFlow.Catalog.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Catalog.Application.Commands.Markets.Deactivate;

public sealed record DeactivateMarketCommand(Guid Id) : ICommand<MarketDto>;
