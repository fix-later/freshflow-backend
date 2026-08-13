using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Commands.OpenMarketSession;

public sealed record OpenMarketSessionCommand(Guid Id, Guid ActorId) : ICommand<MarketSessionDto>;
