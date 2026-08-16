using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Commands.UpdateMarketSession;

public sealed record UpdateMarketSessionCommand(
    Guid Id, DateTimeOffset ClosesAt, Guid ActorId) : ICommand<MarketSessionDto>;
