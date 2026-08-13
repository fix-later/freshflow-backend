using FreshFlow.Procurement.Application.Dtos;
using FreshFlow.SharedKernel.Application;

namespace FreshFlow.Procurement.Application.Commands.CloseMarketSession;

public sealed record CloseMarketSessionCommand(Guid Id, Guid ActorId, string? Reason) : ICommand<MarketSessionDto>;
